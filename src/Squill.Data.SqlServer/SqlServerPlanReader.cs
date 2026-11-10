using Microsoft.Data.SqlClient;
using Squill.Core.Plans;

namespace Squill.Data.SqlServer;

/// <summary>Runs a statement under <c>SET SHOWPLAN_XML ON</c> on an open connection and parses the plan it returns instead of executing.</summary>
internal static class SqlServerPlanReader
{
    /// <summary>Gets the estimated plan of one statement; the statement is compiled, never executed.</summary>
    /// <param name="connection">Open connection; showplan mode is connection state, so it is switched off again before this returns.</param>
    /// <param name="commandTimeoutSeconds">Command timeout in seconds.</param>
    /// <param name="sql">The single statement.</param>
    /// <param name="parameters">Parameters of a normal run.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The parsed plan.</returns>
    /// <exception cref="PlanUnavailableException">Showplan mode could not be switched off (the connection is disposed and the session must be reopened), or the statement has bound parameters, which showplan cannot compile.</exception>
    public static async ValueTask<QueryPlan> ExplainAsync(
        SqlConnection connection, int commandTimeoutSeconds, string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
    {
        string xml;
        try
        {
            await RunAsync(connection, commandTimeoutSeconds, "SET SHOWPLAN_XML ON", [], cancellationToken);
            xml = await RunAsync(connection, commandTimeoutSeconds, sql, parameters, cancellationToken);

            // SqlClient binds parameters through sp_executesql, which showplan mode reports as one EXECUTE PROC statement (or nothing) instead of the compiled query.
            if (parameters.Count > 0 && !xml.Contains("<RelOp", StringComparison.Ordinal))
                throw new PlanUnavailableException("parameters", "SQL Server cannot show the plan of a statement with bound parameters.");
        }
        catch (SqlException ex) when (cancellationToken.IsCancellationRequested)
        {
            // SqlClient reports a cancelled command as a SqlException; callers expect the standard cancellation exception.
            await SwitchOffAsync(connection, commandTimeoutSeconds);
            throw new OperationCanceledException(ex.Message, ex, cancellationToken);
        }
        catch
        {
            await SwitchOffAsync(connection, commandTimeoutSeconds);
            throw;
        }

        await SwitchOffAsync(connection, commandTimeoutSeconds);
        return SqlServerPlanParser.Parse(sql, xml);
    }

    // A connection left in showplan mode would answer every later statement with a plan, so it must never go back to the pool in that state.
    private static async ValueTask SwitchOffAsync(SqlConnection connection, int commandTimeoutSeconds)
    {
        try
        {
            await RunAsync(connection, commandTimeoutSeconds, "SET SHOWPLAN_XML OFF", [], CancellationToken.None);
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException)
        {
            SqlConnection.ClearPool(connection);
            await connection.DisposeAsync();
            throw new PlanUnavailableException("engine", "The SQL Server session must be reopened: showplan mode could not be switched off.");
        }
    }

    private static async Task<string> RunAsync(
        SqlConnection connection, int commandTimeoutSeconds, string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = commandTimeoutSeconds };
        foreach (var parameter in parameters)
            command.Parameters.Add(new SqlParameter("@" + parameter.Name, parameter.Value ?? DBNull.Value));
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }
}
