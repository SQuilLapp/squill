using System.Globalization;
using Microsoft.Data.SqlClient;
using Squill.Core.Activity;

namespace Squill.Data.SqlServer;

/// <summary>Reads the <c>sys.dm_exec_*</c> and <c>sys.dm_tran_*</c> views on a separate session and ends sessions with <c>KILL</c>.</summary>
internal static class SqlServerActivityReader
{
    // Msg 297 / 300 / 262: the login lacks VIEW SERVER STATE (or VIEW SERVER PERFORMANCE STATE on SQL Server 2022).
    private static readonly int[] PermissionErrors = [262, 297, 300];

    private const string SessionsSql = """
        select s.session_id, s.login_name, s.program_name, c.client_net_address, s.status, r.status, r.wait_type, r.wait_resource,
               r.blocking_session_id, left(t.text, 4000), s.is_user_process,
               case when r.session_id is not null then r.total_elapsed_time else datediff_big(millisecond, s.last_request_end_time, getdate()) end,
               todatetimeoffset(tx.begin_time, datepart(tzoffset, sysdatetimeoffset())),
               todatetimeoffset(s.last_request_end_time, datepart(tzoffset, sysdatetimeoffset())),
               coalesce(l.n, 0), case when tx.session_id is not null then 1 else 0 end
        from sys.dm_exec_sessions s
        left join sys.dm_exec_connections c on c.session_id = s.session_id
        left join sys.dm_exec_requests r on r.session_id = s.session_id
        outer apply sys.dm_exec_sql_text(coalesce(r.sql_handle, c.most_recent_sql_handle)) t
        left join (
            select st.session_id, min(at.transaction_begin_time) begin_time
            from sys.dm_tran_session_transactions st
            join sys.dm_tran_active_transactions at on at.transaction_id = st.transaction_id
            group by st.session_id) tx on tx.session_id = s.session_id
        left join (select request_session_id, count(*) n from sys.dm_tran_locks where request_status = 'GRANT' group by request_session_id) l
            on l.request_session_id = s.session_id
        where s.session_id <> @@SPID
        order by s.session_id
        """;

    private const string BlockingSql = """
        select r.blocking_session_id, r.session_id, k.request_mode, r.wait_resource
        from sys.dm_exec_requests r
        outer apply (select top 1 request_mode from sys.dm_tran_locks where request_session_id = r.session_id and request_status = 'WAIT') k
        where r.blocking_session_id > 0 and r.session_id <> @@SPID
        """;

    private const string CountersSql = """
        select @@MAX_CONNECTIONS,
               (select max(cntr_value) from sys.dm_os_performance_counters where counter_name = 'Transactions/sec' and instance_name = '_Total')
        """;

    /// <summary>Takes a snapshot of the server activity.</summary>
    /// <param name="connectionString">Connection string of the monitoring session, separate from the user's.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The snapshot.</returns>
    /// <exception cref="ActivityUnavailableException">The login lacks <c>VIEW SERVER STATE</c>; no partial data is returned.</exception>
    public static async ValueTask<ActivitySnapshot> ReadAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        try
        {
            var sessions = new List<ServerSession>();
            await using (var command = new SqlCommand(SessionsSql, connection))
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                while (await reader.ReadAsync(cancellationToken))
                    sessions.Add(ReadSession(reader));

            var blocking = new List<BlockingEdge>();
            await using (var command = new SqlCommand(BlockingSql, connection))
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                while (await reader.ReadAsync(cancellationToken))
                    blocking.Add(new BlockingEdge(
                        reader.GetInt16(0).ToString(CultureInfo.InvariantCulture),
                        reader.GetInt16(1).ToString(CultureInfo.InvariantCulture),
                        reader.IsDBNull(2) ? null : reader.GetString(2),
                        reader.IsDBNull(3) || reader.GetString(3).Length == 0 ? null : reader.GetString(3)));

            int? maxConnections = null;
            long? counter = null;
            await using (var command = new SqlCommand(CountersSql, connection))
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                if (await reader.ReadAsync(cancellationToken))
                {
                    maxConnections = Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture);
                    counter = reader.IsDBNull(1) ? null : reader.GetInt64(1);
                }

            return new ActivitySnapshot(DateTimeOffset.UtcNow, sessions.Count(s => !s.IsSystem), maxConnections, counter, sessions, blocking, SourceName: "sys.dm_exec_sessions");
        }
        catch (SqlException ex) when (IsPermissionError(ex))
        {
            throw new ActivityUnavailableException("activity.permission", "Reading the server activity needs the VIEW SERVER STATE permission.");
        }
    }

    /// <summary>Ends a session (<c>KILL</c>).</summary>
    /// <param name="connectionString">Connection string of the session that sends the request.</param>
    /// <param name="sessionId">Session id (<c>spid</c>), as text.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="ArgumentException">The id is not a number.</exception>
    /// <exception cref="ActivityActionFailedException">The server refused the request.</exception>
    public static async ValueTask TerminateAsync(string connectionString, string sessionId, CancellationToken cancellationToken)
    {
        var id = ParseId(sessionId);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        // KILL takes no parameter itself, so the numeric id is passed to EXEC as a parameter.
        await using var command = new SqlCommand("EXEC('KILL ' + CAST(@id AS varchar(10)))", connection);
        command.Parameters.Add(new SqlParameter("@id", System.Data.SqlDbType.Int) { Value = id });
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException ex)
        {
            throw new ActivityActionFailedException("refused", ex.Message);
        }
    }

    /// <summary>Parses a session id.</summary>
    /// <param name="sessionId">The text to parse.</param>
    /// <returns>The id.</returns>
    /// <exception cref="ArgumentException">The text is not a positive integer.</exception>
    internal static int ParseId(string sessionId) =>
        int.TryParse(sessionId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
            ? id
            : throw new ArgumentException("A SQL Server session id is a positive number.", nameof(sessionId));

    /// <summary>Maps the session and request columns of the DMVs to the neutral state.</summary>
    /// <param name="requestStatus">Status of the running request, or <see langword="null"/> when the session runs nothing.</param>
    /// <param name="waitType">Current wait type, or <see langword="null"/>.</param>
    /// <param name="blockedBy">Id of the blocking session, or 0.</param>
    /// <param name="inTransaction">The session has an open transaction.</param>
    /// <returns>The neutral state.</returns>
    internal static SessionState MapState(string? requestStatus, string? waitType, int blockedBy, bool inTransaction) => requestStatus switch
    {
        null => inTransaction ? SessionState.IdleInTransaction : SessionState.Idle,
        "background" => SessionState.Other,
        _ when blockedBy > 0 || waitType?.StartsWith("LCK_M_", StringComparison.Ordinal) == true => SessionState.Waiting,
        _ => SessionState.Active,
    };

    /// <summary>Tells whether the error is a missing <c>VIEW SERVER STATE</c> right.</summary>
    /// <param name="error">The error raised by the server.</param>
    /// <returns><see langword="true"/> for a permission error.</returns>
    internal static bool IsPermissionError(SqlException error) => Array.IndexOf(PermissionErrors, error.Number) >= 0;

    private static ServerSession ReadSession(SqlDataReader r)
    {
        var requestStatus = r.IsDBNull(5) ? null : r.GetString(5);
        var waitType = r.IsDBNull(6) ? null : r.GetString(6);
        var blockedBy = r.IsDBNull(8) ? 0 : r.GetInt16(8);
        var inTransaction = r.GetInt32(15) == 1;
        var wait = waitType is null ? null : (r.IsDBNull(7) || r.GetString(7).Length == 0 ? waitType : $"{waitType}:{r.GetString(7)}");
        return new ServerSession(
            r.GetInt16(0).ToString(CultureInfo.InvariantCulture),
            Text(r, 1),
            Text(r, 2),
            Text(r, 3),
            MapState(requestStatus, waitType, blockedBy, inTransaction),
            requestStatus ?? (r.IsDBNull(4) ? string.Empty : r.GetString(4)),
            r.IsDBNull(11) ? null : TimeSpan.FromMilliseconds(r.GetInt64(11)),
            wait,
            Text(r, 9),
            r.IsDBNull(12) ? null : r.GetDateTimeOffset(12),
            r.IsDBNull(13) ? null : r.GetDateTimeOffset(13),
            r.GetInt32(14),
            !r.GetBoolean(10));
    }

    private static string? Text(SqlDataReader r, int ordinal) => r.IsDBNull(ordinal) ? null : (r.GetString(ordinal) is { Length: > 0 } s ? s : null);
}
