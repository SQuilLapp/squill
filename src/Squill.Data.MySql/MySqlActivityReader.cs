using System.Globalization;
using MySqlConnector;
using Squill.Core.Activity;

namespace Squill.Data.MySql;

/// <summary>Reads the process list, <c>innodb_trx</c> and the InnoDB lock tables on a separate session, and kills statements or sessions.</summary>
internal static class MySqlActivityReader
{
    private const string SessionsWithThreadsSql = """
        select p.ID, p.USER, p.HOST, p.DB, p.COMMAND, p.TIME, p.STATE, left(p.INFO, 4000),
               p.USER = 'system user' or coalesce(t.TYPE = 'BACKGROUND', false)
        from information_schema.processlist p
        left join performance_schema.threads t on t.PROCESSLIST_ID = p.ID
        where p.ID <> connection_id()
        order by p.ID
        """;

    private const string SessionsSql = """
        select ID, USER, HOST, DB, COMMAND, TIME, STATE, left(INFO, 4000), USER = 'system user'
        from information_schema.processlist
        where ID <> connection_id()
        order by ID
        """;

    private const string TransactionsSql = "select trx_mysql_thread_id, unix_timestamp(trx_started), trx_state, trx_rows_locked from information_schema.innodb_trx";

    private const string LockWaitsSql = """
        select tb.PROCESSLIST_ID, tw.PROCESSLIST_ID, l.LOCK_MODE, concat(l.OBJECT_SCHEMA, '.', l.OBJECT_NAME)
        from performance_schema.data_lock_waits w
        join performance_schema.threads tb on tb.THREAD_ID = w.BLOCKING_THREAD_ID
        join performance_schema.threads tw on tw.THREAD_ID = w.REQUESTING_THREAD_ID
        left join performance_schema.data_locks l on l.ENGINE_LOCK_ID = w.REQUESTING_ENGINE_LOCK_ID
        """;

    // MariaDB and MySQL 5.7 have no data_lock_waits; the sys view exists on both, without a relation name that is stable across versions.
    private const string SysLockWaitsSql = "select blocking_pid, waiting_pid, waiting_lock_mode, null from sys.innodb_lock_waits";

    private const string StatusSql = "show global status where Variable_name in ('Threads_connected', 'Com_commit', 'Com_rollback')";

    /// <summary>Takes a snapshot of the server activity.</summary>
    /// <param name="connectionString">Connection string of the monitoring session, separate from the user's.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The snapshot.</returns>
    public static async ValueTask<ActivitySnapshot> ReadAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var rows = await TryReadAsync(connection, SessionsWithThreadsSql, ReadRow, cancellationToken)
            ?? await TryReadAsync(connection, SessionsSql, ReadRow, cancellationToken)
            ?? [];
        // innodb_trx needs the PROCESS right: without it the sessions simply carry no transaction information.
        var transactions = (await TryReadAsync(connection, TransactionsSql, ReadTransaction, cancellationToken) ?? []).ToDictionary(t => t.ThreadId);

        var sessions = new List<ServerSession>(rows.Count);
        foreach (var row in rows)
        {
            transactions.TryGetValue(row.Id, out var trx);
            sessions.Add(new ServerSession(
                row.Id.ToString(CultureInfo.InvariantCulture),
                row.User,
                null,
                row.Host,
                MapState(row.Command, trx is not null, trx?.LockWait == true),
                StateText(row.Command, row.State),
                row.Seconds is { } s ? TimeSpan.FromSeconds(s) : null,
                trx?.LockWait == true ? "InnoDB lock wait" : null,
                row.Info,
                trx?.Started,
                null,
                trx?.RowsLocked,
                row.IsSystem));
        }

        var (blocking, unavailable) = await ReadBlockingAsync(connection, cancellationToken);
        int? maxConnections = null;
        await using (var command = new MySqlCommand("select @@max_connections", connection))
            if (await command.ExecuteScalarAsync(cancellationToken) is { } max and not DBNull)
                maxConnections = Convert.ToInt32(max, CultureInfo.InvariantCulture);

        var status = (await TryReadAsync(connection, StatusSql, r => (Name: r.GetString(0), Value: long.Parse(r.GetString(1), CultureInfo.InvariantCulture)), cancellationToken) ?? [])
            .ToDictionary(x => x.Name, x => x.Value, StringComparer.OrdinalIgnoreCase);
        long? counter = status.TryGetValue("Com_commit", out var commits) && status.TryGetValue("Com_rollback", out var rollbacks) ? commits + rollbacks : null;
        var active = status.TryGetValue("Threads_connected", out var connected) ? (int)connected : sessions.Count(s => !s.IsSystem);
        return new ActivitySnapshot(DateTimeOffset.UtcNow, active, maxConnections, counter, sessions, blocking, unavailable);
    }

    /// <summary>Cancels the statement of a session (<c>KILL QUERY</c>).</summary>
    /// <param name="connectionString">Connection string of the session that sends the request.</param>
    /// <param name="sessionId">Connection id, as text.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="ArgumentException">The id is not a number.</exception>
    /// <exception cref="ActivityActionFailedException">The server refused the request.</exception>
    public static ValueTask CancelAsync(string connectionString, string sessionId, CancellationToken cancellationToken) =>
        KillAsync(connectionString, "KILL QUERY ", sessionId, cancellationToken);

    /// <summary>Ends a session (<c>KILL</c>).</summary>
    /// <param name="connectionString">Connection string of the session that sends the request.</param>
    /// <param name="sessionId">Connection id, as text.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="ArgumentException">The id is not a number.</exception>
    /// <exception cref="ActivityActionFailedException">The server refused the request.</exception>
    public static ValueTask TerminateAsync(string connectionString, string sessionId, CancellationToken cancellationToken) =>
        KillAsync(connectionString, "KILL ", sessionId, cancellationToken);

    /// <summary>Parses a connection id.</summary>
    /// <param name="sessionId">The text to parse.</param>
    /// <returns>The id.</returns>
    /// <exception cref="ArgumentException">The text is not a positive integer.</exception>
    internal static long ParseId(string sessionId) =>
        long.TryParse(sessionId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
            ? id
            : throw new ArgumentException("A MySQL session id is a positive number.", nameof(sessionId));

    /// <summary>Maps the <c>COMMAND</c> of the process list to the neutral state.</summary>
    /// <param name="command">The engine command (<c>Sleep</c>, <c>Query</c>, ...).</param>
    /// <param name="inTransaction">The session has an open InnoDB transaction.</param>
    /// <param name="waitingForLock">The open transaction waits for a lock.</param>
    /// <returns>The neutral state.</returns>
    internal static SessionState MapState(string? command, bool inTransaction, bool waitingForLock) => command switch
    {
        "Sleep" => inTransaction ? SessionState.IdleInTransaction : SessionState.Idle,
        "Query" or "Execute" or "Prepare" or "Init DB" or "Statistics" => waitingForLock ? SessionState.Waiting : SessionState.Active,
        _ => SessionState.Other,
    };

    /// <summary>Builds the display wording of a state from the <c>COMMAND</c> and <c>STATE</c> columns.</summary>
    /// <param name="command">The engine command.</param>
    /// <param name="state">The engine state, or <see langword="null"/>.</param>
    /// <returns>The text.</returns>
    internal static string StateText(string? command, string? state) =>
        string.IsNullOrEmpty(state) ? command ?? string.Empty : $"{command}: {state}";

    private static async ValueTask KillAsync(string connectionString, string verb, string sessionId, CancellationToken cancellationToken)
    {
        var id = ParseId(sessionId);
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        // KILL takes no parameter: the id was parsed as a number above, so the concatenation cannot carry anything else.
        await using var command = new MySqlCommand(verb + id.ToString(CultureInfo.InvariantCulture), connection);
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (MySqlException ex)
        {
            throw new ActivityActionFailedException("refused", ex.Message);
        }
    }

    private static async ValueTask<(List<BlockingEdge> Edges, bool Unavailable)> ReadBlockingAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        foreach (var sql in new[] { LockWaitsSql, SysLockWaitsSql })
            if (await TryReadAsync(connection, sql, ReadEdge, cancellationToken) is { } edges)
                return (edges, false);
        return ([], true);
    }

    private static BlockingEdge ReadEdge(MySqlDataReader r) => new(
        Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
        Convert.ToInt64(r.GetValue(1), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
        Text(r, 2),
        Text(r, 3));

    private static SessionRow ReadRow(MySqlDataReader r) => new(
        Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
        Text(r, 1),
        Text(r, 2),
        Text(r, 4),
        Text(r, 6),
        r.IsDBNull(5) ? null : Convert.ToInt64(r.GetValue(5), CultureInfo.InvariantCulture),
        Text(r, 7),
        Convert.ToInt64(r.GetValue(8), CultureInfo.InvariantCulture) != 0);

    private static TransactionRow ReadTransaction(MySqlDataReader r) => new(
        Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
        r.IsDBNull(1) ? null : DateTimeOffset.FromUnixTimeSeconds((long)Convert.ToDecimal(r.GetValue(1), CultureInfo.InvariantCulture)),
        r.GetString(2) == "LOCK WAIT",
        r.IsDBNull(3) ? null : (int)Math.Min(int.MaxValue, Convert.ToInt64(r.GetValue(3), CultureInfo.InvariantCulture)));

    private static string? Text(MySqlDataReader r, int ordinal) => r.IsDBNull(ordinal) ? null : (r.GetString(ordinal) is { Length: > 0 } s ? s : null);

    // Returns null when the server refuses the query (missing right, missing table or view): callers fall back or report "not available".
    private static async ValueTask<List<T>?> TryReadAsync<T>(MySqlConnection connection, string sql, Func<MySqlDataReader, T> map, CancellationToken cancellationToken)
    {
        try
        {
            var result = new List<T>();
            await using var command = new MySqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                result.Add(map(reader));
            return result;
        }
        catch (MySqlException)
        {
            return null;
        }
    }

    private sealed record SessionRow(long Id, string? User, string? Host, string? Command, string? State, long? Seconds, string? Info, bool IsSystem);

    private sealed record TransactionRow(long ThreadId, DateTimeOffset? Started, bool LockWait, int? RowsLocked);
}
