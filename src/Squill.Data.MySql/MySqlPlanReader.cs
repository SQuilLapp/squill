using System.Globalization;
using MySqlConnector;
using Squill.Core.Plans;

namespace Squill.Data.MySql;

/// <summary>Runs <c>EXPLAIN FORMAT=JSON</c> on an open MySQL connection and parses the answer.</summary>
internal static class MySqlPlanReader
{
    /// <summary>Gets the estimated plan of one statement; the statement is never executed.</summary>
    /// <param name="connection">Open connection; the statement is explained on this session.</param>
    /// <param name="commandTimeoutSeconds">Command timeout in seconds.</param>
    /// <param name="sql">The single statement.</param>
    /// <param name="parameters">Parameters of a normal run.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The parsed plan.</returns>
    /// <exception cref="PlanUnavailableException">The server is MariaDB, whose plan format differs, or refuses <c>EXPLAIN</c> with bound parameters.</exception>
    public static async ValueTask<QueryPlan> ExplainAsync(
        MySqlConnection connection, int commandTimeoutSeconds, string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
    {
        if (connection.ServerVersion.Contains("MariaDB", StringComparison.OrdinalIgnoreCase))
            throw new PlanUnavailableException("engine", "MariaDB servers are not supported for query plans.");
        var json = await RunAsync(connection, commandTimeoutSeconds, "EXPLAIN FORMAT=JSON " + sql, parameters, cancellationToken);
        return MySqlPlanParser.Parse(sql, json);
    }

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
