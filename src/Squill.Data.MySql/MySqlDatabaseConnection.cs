using MySqlConnector;
using Squill.Core;
using Squill.Core.Plans;
using Squill.Core.Transactions;

namespace Squill.Data.MySql;

/// <summary>MySQL implementation of <see cref="IDatabaseConnection"/> over one open connection.</summary>
internal sealed class MySqlDatabaseConnection(MySqlConnection connection, int commandTimeoutSeconds) : IDatabaseConnection, IPlanReader, ITransactionProbe
{
    /// <inheritdoc />
    public string ServerVersion => connection.ServerVersion;

    /// <inheritdoc />
    public async ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
    {
        var command = new MySqlCommand(sql, connection) { CommandTimeout = commandTimeoutSeconds };
        var messages = new List<string>();
        MySqlInfoMessageEventHandler handler = (_, a) => { foreach (var e in a.Errors) messages.Add(e.Message); };
        connection.InfoMessage += handler;
        try
        {
            foreach (var parameter in parameters)
                command.Parameters.Add(new MySqlParameter(parameter.Name, parameter.Value ?? DBNull.Value));
            var reader = await command.ExecuteReaderAsync(cancellationToken);
            return new MySqlResultReader(command, reader, messages, () => connection.InfoMessage -= handler);
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
        MySqlPlanReader.ExplainAsync(connection, commandTimeoutSeconds, sql, parameters, mode, cancellationToken);

    /// <inheritdoc />
    public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) =>
        MySqlSchemaReader.ReadAsync(connection, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken)
    {
        await using var reader = await ExecuteAsync("select schema_name from information_schema.schemata where schema_name not in ('information_schema','mysql','performance_schema','sys') order by 1", [], cancellationToken);
        return [.. (await reader.ReadAllAsync(256, cancellationToken)).Select(r => (string)r[0]!)];
    }

    /// <inheritdoc />
    /// <remarks>
    /// Always unknown: MySQL 8.4 has no "am I in a transaction" variable (<c>@@in_transaction</c> is MariaDB), <c>performance_schema.events_transactions_current</c>
    /// needs a SELECT grant that ordinary users lack and <c>information_schema.innodb_trx</c> needs PROCESS and misses read-only transactions (proven in the tests).
    /// </remarks>
    public ValueTask<TransactionProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(TransactionProbeResult.Unknown);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // MySqlConnector pools the connection: an open transaction (and its locks) would otherwise survive until the next user of the pool resets it.
        try
        {
            await using var rollback = new MySqlCommand("ROLLBACK", connection) { CommandTimeout = commandTimeoutSeconds };
            await rollback.ExecuteNonQueryAsync();
        }
        catch (Exception ex) when (ex is MySqlException or InvalidOperationException)
        {
            // Closing the connection below is what matters.
        }

        await connection.DisposeAsync();
    }
}
