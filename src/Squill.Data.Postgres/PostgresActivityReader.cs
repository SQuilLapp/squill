using System.Globalization;
using Npgsql;
using Squill.Core.Activity;

namespace Squill.Data.Postgres;

/// <summary>Reads <c>pg_stat_activity</c>, <c>pg_blocking_pids</c> and <c>pg_locks</c> on a separate pooled session, and cancels or ends backends.</summary>
internal static class PostgresActivityReader
{
    private const string Insufficient = "<insufficient privilege>";

    private const string SessionsSql = """
        select a.pid, a.usename, a.application_name, host(a.client_addr), a.client_port, a.state, a.wait_event_type, a.wait_event,
               left(a.query, 4000), a.xact_start, a.state_change,
               extract(epoch from now() - case when a.state = 'active' then a.query_start when a.state like 'idle in transaction%' then coalesce(a.xact_start, a.state_change) else a.state_change end)::float8,
               a.backend_type, coalesce(l.n, 0)::int
        from pg_stat_activity a
        left join (select pid, count(*) n from pg_locks where granted group by pid) l on l.pid = a.pid
        where a.pid <> pg_backend_pid()
        order by a.pid
        """;

    private const string BlockingSql = """
        select b.blocker, a.pid, w.mode, case when w.relation is not null then w.relation::regclass::text end
        from pg_stat_activity a
        cross join lateral unnest(pg_blocking_pids(a.pid)) as b(blocker)
        left join lateral (
            -- A row-level wait is on a transactionid (no relation); the waiter's granted tuple lock names the table.
            select mode, coalesce(relation, (select t.relation from pg_locks t where t.pid = a.pid and t.locktype = 'tuple' limit 1)) as relation
            from pg_locks where pid = a.pid and not granted order by relation is null limit 1) w on true
        where a.pid <> pg_backend_pid()
        """;

    private const string CountersSql = """
        select current_setting('max_connections')::int,
               (select xact_commit + xact_rollback from pg_stat_database where datname = current_database())
        """;

    /// <summary>Takes a snapshot of the server activity.</summary>
    /// <param name="dataSource">Source of the monitoring session, separate from the user's.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The snapshot.</returns>
    public static async ValueTask<ActivitySnapshot> ReadAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var sessions = new List<ServerSession>();
        await using (var command = new NpgsqlCommand(SessionsSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
                sessions.Add(ReadSession(reader));

        var blocking = new List<BlockingEdge>();
        await using (var command = new NpgsqlCommand(BlockingSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken))
                blocking.Add(new BlockingEdge(
                    reader.GetInt32(0).ToString(CultureInfo.InvariantCulture),
                    reader.GetInt32(1).ToString(CultureInfo.InvariantCulture),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3)));

        int? maxConnections = null;
        long? counter = null;
        await using (var command = new NpgsqlCommand(CountersSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            if (await reader.ReadAsync(cancellationToken))
            {
                maxConnections = reader.GetInt32(0);
                counter = reader.IsDBNull(1) ? null : reader.GetInt64(1);
            }

        return new ActivitySnapshot(DateTimeOffset.UtcNow, sessions.Count(s => !s.IsSystem), maxConnections, counter, sessions, blocking);
    }

    /// <summary>Cancels the statement of a backend.</summary>
    /// <param name="dataSource">Source of the session that sends the request.</param>
    /// <param name="sessionId">Backend pid, as text.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="ArgumentException">The id is not a number.</exception>
    /// <exception cref="ActivityActionFailedException">The server answered false.</exception>
    public static ValueTask CancelAsync(NpgsqlDataSource dataSource, string sessionId, CancellationToken cancellationToken) =>
        SignalAsync(dataSource, "select pg_cancel_backend($1)", sessionId, cancellationToken);

    /// <summary>Terminates a backend.</summary>
    /// <param name="dataSource">Source of the session that sends the request.</param>
    /// <param name="sessionId">Backend pid, as text.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="ArgumentException">The id is not a number.</exception>
    /// <exception cref="ActivityActionFailedException">The server answered false.</exception>
    public static ValueTask TerminateAsync(NpgsqlDataSource dataSource, string sessionId, CancellationToken cancellationToken) =>
        SignalAsync(dataSource, "select pg_terminate_backend($1)", sessionId, cancellationToken);

    /// <summary>Parses a backend pid.</summary>
    /// <param name="sessionId">The text to parse.</param>
    /// <returns>The pid.</returns>
    /// <exception cref="ArgumentException">The text is not a positive integer.</exception>
    internal static int ParsePid(string sessionId) =>
        int.TryParse(sessionId, NumberStyles.None, CultureInfo.InvariantCulture, out var pid) && pid > 0
            ? pid
            : throw new ArgumentException("A PostgreSQL session id is a positive number.", nameof(sessionId));

    /// <summary>Maps the <c>state</c> and wait type of <c>pg_stat_activity</c> to the neutral state.</summary>
    /// <param name="state">The engine state, or <see langword="null"/>.</param>
    /// <param name="waitEventType">The wait event type, or <see langword="null"/>.</param>
    /// <returns>The neutral state.</returns>
    internal static SessionState MapState(string? state, string? waitEventType) => state switch
    {
        "active" when waitEventType == "Lock" => SessionState.Waiting,
        "active" => SessionState.Active,
        "idle" => SessionState.Idle,
        not null when state.StartsWith("idle in transaction", StringComparison.Ordinal) => SessionState.IdleInTransaction,
        _ => SessionState.Other,
    };

    private static async ValueTask SignalAsync(NpgsqlDataSource dataSource, string sql, string sessionId, CancellationToken cancellationToken)
    {
        var pid = ParsePid(sessionId);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(new NpgsqlParameter { Value = pid });
        if (await command.ExecuteScalarAsync(cancellationToken) is not true)
            throw new ActivityActionFailedException("refused", $"The server refused the request for session {pid}.");
    }

    private static ServerSession ReadSession(NpgsqlDataReader r)
    {
        var state = Text(r, 5);
        var waitType = Text(r, 6);
        var wait = waitType is null ? null : (Text(r, 7) is { } name ? $"{waitType}:{name}" : waitType);
        var client = Text(r, 3) is { } host ? (r.IsDBNull(4) ? host : $"{host}:{r.GetInt32(4).ToString(CultureInfo.InvariantCulture)}") : null;
        var query = Text(r, 8);
        return new ServerSession(
            r.GetInt32(0).ToString(CultureInfo.InvariantCulture),
            Text(r, 1),
            Text(r, 2),
            client,
            MapState(state, waitType),
            state ?? string.Empty,
            r.IsDBNull(11) ? null : TimeSpan.FromSeconds(r.GetDouble(11)),
            wait,
            query == Insufficient ? null : query,
            r.IsDBNull(9) ? null : new DateTimeOffset(r.GetDateTime(9)),
            r.IsDBNull(10) ? null : new DateTimeOffset(r.GetDateTime(10)),
            r.GetInt32(13),
            Text(r, 12) != "client backend");
    }

    private static string? Text(NpgsqlDataReader r, int ordinal) => r.IsDBNull(ordinal) ? null : (r.GetString(ordinal) is { Length: > 0 } s ? s : null);
}
