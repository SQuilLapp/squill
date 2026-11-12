using Microsoft.Data.SqlClient;
using Squill.Core;
using Squill.Core.Plans;
using Squill.Core.Transactions;

namespace Squill.Data.SqlServer;

/// <summary>SQL Server implementation of <see cref="IDatabaseConnection"/> over one open connection.</summary>
internal sealed class SqlServerDatabaseConnection(SqlConnection connection, int commandTimeoutSeconds) : IDatabaseConnection, IPlanReader, ITransactionProbe
{
    /// <inheritdoc />
    public string ServerVersion => connection.ServerVersion;

    /// <inheritdoc />
    public async ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
    {
        var command = new SqlCommand(sql, connection) { CommandTimeout = commandTimeoutSeconds };
        var messages = new List<string>();
        SqlInfoMessageEventHandler handler = (_, a) => { foreach (SqlError e in a.Errors) messages.Add(e.Message); };
        connection.InfoMessage += handler;
        try
        {
            foreach (var parameter in parameters)
                command.Parameters.Add(new SqlParameter("@" + parameter.Name, parameter.Value ?? DBNull.Value));
            var reader = await command.ExecuteReaderAsync(cancellationToken);
            return new SqlServerResultReader(command, reader, messages, () => connection.InfoMessage -= handler);
        }
        catch (SqlException ex) when (cancellationToken.IsCancellationRequested)
        {
            // SqlClient reports a cancelled command as a SqlException; callers expect the standard cancellation exception.
            connection.InfoMessage -= handler;
            await command.DisposeAsync();
            throw new OperationCanceledException(ex.Message, ex, cancellationToken);
        }
        catch
        {
            connection.InfoMessage -= handler;
            await command.DisposeAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public bool SupportsActualPlan => true;

    /// <inheritdoc />
    public ValueTask<QueryPlan> ExplainAsync(string sql, IReadOnlyList<QueryParameter> parameters, PlanMode mode, CancellationToken cancellationToken) =>
        SqlServerPlanReader.ExplainAsync(connection, commandTimeoutSeconds, sql, parameters, mode, cancellationToken);

    /// <inheritdoc />
    public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) =>
        SqlServerSchemaReader.ReadAsync(connection, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken)
    {
        await using var reader = await ExecuteAsync("select name from sys.databases where state = 0 and name not in ('master','tempdb','model','msdb') order by 1", [], cancellationToken);
        return [.. (await reader.ReadAllAsync(256, cancellationToken)).Select(r => (string)r[0]!)];
    }

    /// <inheritdoc />
    public async ValueTask<TransactionProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var command = new SqlCommand("SELECT @@TRANCOUNT, XACT_STATE()", connection) { CommandTimeout = commandTimeoutSeconds };
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return TransactionProbeResult.Unknown;
            var transactions = Convert.ToInt32(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture);
            var state = Convert.ToInt32(reader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture);
            // XACT_STATE() = -1: doomed (uncommittable), only a rollback is possible.
            return new TransactionProbeResult(transactions > 0, state == -1);
        }
        catch (SqlException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException)
        {
            return TransactionProbeResult.Unknown;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // The pool only resets the connection on its next use: an open transaction would keep its locks until then.
        try
        {
            await using var rollback = new SqlCommand("IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION", connection) { CommandTimeout = commandTimeoutSeconds };
            await rollback.ExecuteNonQueryAsync();
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException)
        {
            // Closing the connection below is what matters.
        }

        await connection.DisposeAsync();
    }
}
