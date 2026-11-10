using System.Globalization;
using Microsoft.Data.SqlClient;
using Squill.Core.Plans;

namespace Squill.Data.SqlServer;

/// <summary>Runs a statement under <c>SET SHOWPLAN_XML ON</c> (compiled, not executed) or <c>SET STATISTICS XML ON</c> (executed in a rolled-back transaction) and parses the plan XML.</summary>
internal static class SqlServerPlanReader
{
    private const string PlanColumn = "Microsoft SQL Server 2005 XML Showplan";
    private const string SavePoint = "squill_plan";

    /// <summary>Gets the plan of one statement.</summary>
    /// <param name="connection">Open connection; showplan and statistics mode are connection state, so they are switched off again before this returns.</param>
    /// <param name="commandTimeoutSeconds">Command timeout in seconds.</param>
    /// <param name="sql">The single statement.</param>
    /// <param name="parameters">Parameters of a normal run.</param>
    /// <param name="mode">Estimated compiles only; actual executes the statement and rolls its effects back.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The parsed plan.</returns>
    /// <exception cref="PlanUnavailableException">The mode could not be switched off or the transaction not rolled back (the connection is disposed and the session must be reopened), or the statement has bound parameters, which showplan cannot compile.</exception>
    public static async ValueTask<QueryPlan> ExplainAsync(
        SqlConnection connection, int commandTimeoutSeconds, string sql, IReadOnlyList<QueryParameter> parameters, PlanMode mode, CancellationToken cancellationToken)
    {
        var actual = mode == PlanMode.Actual;
        var option = actual ? "STATISTICS XML" : "SHOWPLAN_XML";
        var transaction = false;
        var begun = false;
        string xml;
        try
        {
            if (actual)
            {
                // A user transaction may be open: a savepoint lets the rollback undo only what the statement did.
                var open = Convert.ToInt32(await ScalarAsync(connection, commandTimeoutSeconds, "select @@TRANCOUNT", cancellationToken), CultureInfo.InvariantCulture) > 0;
                await ScalarAsync(connection, commandTimeoutSeconds, open ? $"SAVE TRANSACTION {SavePoint}" : "BEGIN TRANSACTION", cancellationToken);
                (transaction, begun) = (true, !open);
            }

            await ScalarAsync(connection, commandTimeoutSeconds, $"SET {option} ON", cancellationToken);
            xml = await PlanXmlAsync(connection, commandTimeoutSeconds, sql, parameters, cancellationToken);

            // SqlClient binds parameters through sp_executesql, which showplan mode reports as one EXECUTE PROC statement (or nothing) instead of the compiled query.
            if (parameters.Count > 0 && !xml.Contains("<RelOp", StringComparison.Ordinal))
                throw new PlanUnavailableException("parameters", "SQL Server cannot show the plan of a statement with bound parameters.");
        }
        catch (SqlException ex) when (cancellationToken.IsCancellationRequested)
        {
            // SqlClient reports a cancelled command as a SqlException; callers expect the standard cancellation exception.
            await RestoreAsync(connection, commandTimeoutSeconds, option, transaction, begun);
            throw new OperationCanceledException(ex.Message, ex, cancellationToken);
        }
        catch
        {
            await RestoreAsync(connection, commandTimeoutSeconds, option, transaction, begun);
            throw;
        }

        await RestoreAsync(connection, commandTimeoutSeconds, option, transaction, begun);
        return SqlServerPlanParser.Parse(sql, xml, mode) with { RolledBack = actual };
    }

    // A connection left in showplan mode would answer every later statement with a plan, and one left in the wrapper transaction would keep the statement's effects:
    // neither may go back to the pool, so a failure here disposes the connection.
    private static async ValueTask RestoreAsync(SqlConnection connection, int commandTimeoutSeconds, string option, bool transaction, bool begun)
    {
        try
        {
            await ScalarAsync(connection, commandTimeoutSeconds, $"SET {option} OFF", CancellationToken.None);
            if (transaction)
                await ScalarAsync(connection, commandTimeoutSeconds, begun ? "ROLLBACK TRANSACTION" : $"ROLLBACK TRANSACTION {SavePoint}", CancellationToken.None);
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException)
        {
            SqlConnection.ClearPool(connection);
            await connection.DisposeAsync();
            throw new PlanUnavailableException("engine", "The SQL Server session must be reopened: the plan mode could not be switched off.");
        }
    }

    private static SqlCommand Command(SqlConnection connection, int commandTimeoutSeconds, string sql, IReadOnlyList<QueryParameter> parameters)
    {
        var command = new SqlCommand(sql, connection) { CommandTimeout = commandTimeoutSeconds };
        foreach (var parameter in parameters)
            command.Parameters.Add(new SqlParameter("@" + parameter.Name, parameter.Value ?? DBNull.Value));
        return command;
    }

    private static async Task<object?> ScalarAsync(SqlConnection connection, int commandTimeoutSeconds, string sql, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, commandTimeoutSeconds, sql, []);
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    // The plan is the last result set whose column is the XML showplan one; the statement's own result sets (actual mode) are drained and discarded.
    private static async Task<string> PlanXmlAsync(
        SqlConnection connection, int commandTimeoutSeconds, string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
    {
        await using var command = Command(connection, commandTimeoutSeconds, sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var xml = "";
        do
        {
            var isPlan = reader.FieldCount == 1 && reader.GetName(0) == PlanColumn;
            while (await reader.ReadAsync(cancellationToken))
            {
                if (isPlan)
                    xml = Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "";
            }
        }
        while (await reader.NextResultAsync(cancellationToken));
        return xml;
    }
}
