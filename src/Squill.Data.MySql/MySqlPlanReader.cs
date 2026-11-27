using System.Globalization;
using MySqlConnector;
using Squill.Core.Plans;

namespace Squill.Data.MySql;

/// <summary>Runs <c>EXPLAIN FORMAT=JSON</c> or <c>EXPLAIN ANALYZE</c> (MariaDB: <c>ANALYZE FORMAT=JSON</c>) on an open MySQL or MariaDB connection and parses the answer.</summary>
internal static class MySqlPlanReader
{
    private static readonly Version MinimumAnalyzeVersion = new(8, 0, 18);

    /// <summary>Gets the plan of one statement: estimated (never executed) or actual (<c>EXPLAIN ANALYZE</c> runs it; the guard of the caller allows read-only statements only).</summary>
    /// <param name="connection">Open connection; the statement is explained on this session.</param>
    /// <param name="commandTimeoutSeconds">Command timeout in seconds.</param>
    /// <param name="sql">The single statement.</param>
    /// <param name="parameters">Parameters of a normal run.</param>
    /// <param name="mode">Estimated or actual.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The parsed plan.</returns>
    /// <exception cref="PlanUnavailableException">The server is MySQL than 8.0.18 for an actual plan, or refuses <c>EXPLAIN</c> with bound parameters.</exception>
    public static async ValueTask<QueryPlan> ExplainAsync(
        MySqlConnection connection, int commandTimeoutSeconds, string sql, IReadOnlyList<QueryParameter> parameters, PlanMode mode, CancellationToken cancellationToken)
    {
        if (IsMariaDb(connection))
        {
            var analyze = mode == PlanMode.Actual;
            return MariaDbPlanParser.Parse(sql, await RunAsync(connection, commandTimeoutSeconds, (analyze ? "ANALYZE FORMAT=JSON " : "EXPLAIN FORMAT=JSON ") + sql, parameters, cancellationToken), mode);
        }
        if (mode == PlanMode.Estimated)
            return MySqlPlanParser.Parse(sql, await RunAsync(connection, commandTimeoutSeconds, "EXPLAIN FORMAT=JSON " + sql, parameters, cancellationToken));

        if (Version.TryParse(connection.ServerVersion.Split('-')[0], out var version) && version < MinimumAnalyzeVersion)
            throw new PlanUnavailableException("version", "EXPLAIN ANALYZE needs MySQL 8.0.18 or later.");
        return MySqlAnalyzeTextParser.Parse(sql, await RunAsync(connection, commandTimeoutSeconds, "EXPLAIN ANALYZE " + sql, parameters, cancellationToken));
    }

    /// <summary>Whether the server is MariaDB (MySqlConnector reports <c>11.4.x-MariaDB</c>).</summary>
    internal static bool IsMariaDb(MySqlConnection connection) => connection.ServerVersion.Contains("MariaDB", StringComparison.OrdinalIgnoreCase);

    /// <summary>Runs one <c>EXPLAIN</c> statement and returns its single text cell.</summary>
    internal static async Task<string> RunAsync(
        MySqlConnection connection, int commandTimeoutSeconds, string explain, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(explain, connection) { CommandTimeout = commandTimeoutSeconds };
        foreach (var parameter in parameters)
            command.Parameters.Add(new MySqlParameter(parameter.Name, parameter.Value ?? DBNull.Value));
        try
        {
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToString(result, CultureInfo.InvariantCulture) ?? "";
        }
        catch (MySqlException ex) when (parameters.Count > 0 && ex.ErrorCode == MySqlErrorCode.ParseError && ex.Message.Contains('?', StringComparison.Ordinal))
        {
            throw new PlanUnavailableException("parameters", ex.Message);
        }
    }
}
