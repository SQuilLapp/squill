using Npgsql;
using Squill.Core.Plans;

namespace Squill.Data.Postgres;

/// <summary>Runs <c>EXPLAIN (FORMAT JSON)</c> or <c>EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)</c> on an open connection and parses the answer.</summary>
internal static class PostgresPlanReader
{
    private const string ActiveTransactionState = "25001";
    private const string FeatureNotSupportedState = "0A000";

    /// <summary>Gets the plan of one statement.</summary>
    /// <param name="connection">Open connection; the statement runs on this session.</param>
    /// <param name="commandTimeoutSeconds">Command timeout in seconds.</param>
    /// <param name="sql">The single statement.</param>
    /// <param name="parameters">Parameters of a normal run.</param>
    /// <param name="mode">Estimated (never executes) or actual (executes inside a read-only transaction that is rolled back).</param>
    /// <param name="cancellationToken">Cancels the request; the connection stays usable.</param>
    /// <returns>The parsed plan.</returns>
    public static async ValueTask<QueryPlan> ExplainAsync(
        NpgsqlConnection connection, int commandTimeoutSeconds, string sql, IReadOnlyList<QueryParameter> parameters, PlanMode mode, CancellationToken cancellationToken)
    {
        if (mode == PlanMode.Estimated)
            return PostgresPlanParser.Parse(sql, mode, await RunExplainAsync(connection, commandTimeoutSeconds, "EXPLAIN (FORMAT JSON) " + sql, parameters, cancellationToken));

        // BEGIN READ ONLY inside a user transaction only raises warning 25001: that transaction is the user's, so it must not be rolled back.
        var userTransaction = false;
        NoticeEventHandler handler = (_, a) => userTransaction |= a.Notice.SqlState == ActiveTransactionState;
        connection.Notice += handler;
        try
        {
            await ExecuteAsync(connection, commandTimeoutSeconds, "BEGIN READ ONLY", cancellationToken);
            try
            {
                var plan = PostgresPlanParser.Parse(sql, mode, await RunExplainAsync(connection, commandTimeoutSeconds, "EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + sql, parameters, cancellationToken));
                return plan with { RolledBack = !userTransaction };
            }
            finally
            {
                if (!userTransaction)
                    await ExecuteAsync(connection, commandTimeoutSeconds, "ROLLBACK", CancellationToken.None);
            }
        }
        finally
        {
            connection.Notice -= handler;
        }
    }

    private static async Task<string> RunExplainAsync(
        NpgsqlConnection connection, int commandTimeoutSeconds, string explain, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(explain, connection) { CommandTimeout = commandTimeoutSeconds };
        foreach (var parameter in parameters)
            command.Parameters.Add(new NpgsqlParameter(parameter.Name, parameter.Value ?? DBNull.Value));
        try
        {
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToString(result, System.Globalization.CultureInfo.InvariantCulture) ?? "";
        }
        catch (PostgresException ex) when (parameters.Count > 0 && ex.SqlState == FeatureNotSupportedState)
        {
            throw new PlanUnavailableException("parameters", ex.MessageText);
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, int commandTimeoutSeconds, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = commandTimeoutSeconds };
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
