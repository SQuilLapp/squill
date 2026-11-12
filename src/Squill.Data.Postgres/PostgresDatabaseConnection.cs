using Npgsql;
using Squill.Core;
using Squill.Core.Plans;
using Squill.Core.Transactions;

namespace Squill.Data.Postgres;

/// <summary>Postgres implementation of <see cref="IDatabaseConnection"/> over one open connection.</summary>
internal sealed class PostgresDatabaseConnection(NpgsqlDataSource dataSource, NpgsqlConnection connection, int commandTimeoutSeconds) : IDatabaseConnection, IPlanReader, ITransactionProbe
{
    /// <inheritdoc />
    public string ServerVersion => connection.PostgreSqlVersion.ToString();

    /// <inheritdoc />
    public async ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(sql, connection) { CommandTimeout = commandTimeoutSeconds };
        var messages = new List<string>();
        NoticeEventHandler handler = (_, a) => { messages.Add(a.Notice.MessageText); };
        connection.Notice += handler;
        try
        {
            foreach (var parameter in parameters)
                command.Parameters.Add(new NpgsqlParameter(parameter.Name, parameter.Value ?? DBNull.Value));
            var reader = await command.ExecuteReaderAsync(cancellationToken);
            return new PostgresResultReader(command, reader, messages, () => connection.Notice -= handler);
        }
        catch
        {
            connection.Notice -= handler;
            await command.DisposeAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public bool SupportsActualPlan => true;

    /// <inheritdoc />
    public ValueTask<QueryPlan> ExplainAsync(string sql, IReadOnlyList<QueryParameter> parameters, PlanMode mode, CancellationToken cancellationToken) =>
        PostgresPlanReader.ExplainAsync(connection, commandTimeoutSeconds, sql, parameters, mode, cancellationToken);

    /// <inheritdoc />
    public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) =>
        PostgresSchemaReader.ReadAsync(connection, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken)
    {
        await using var reader = await ExecuteAsync("select datname from pg_database where not datistemplate and datallowconn order by 1", [], cancellationToken);
        return [.. (await reader.ReadAllAsync(256, cancellationToken)).Select(r => (string)r[0]!)];
    }

    /// <inheritdoc />
    public async ValueTask<TransactionProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        // Npgsql exposes no public transaction status: a SELECT 1 only tells whether the transaction is aborted (25P02).
        try
        {
            await using var command = new NpgsqlCommand("SELECT 1", connection) { CommandTimeout = commandTimeoutSeconds };
            await command.ExecuteScalarAsync(cancellationToken);
            return TransactionProbeResult.Unknown;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InFailedSqlTransaction)
        {
            return new TransactionProbeResult(true, true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return TransactionProbeResult.Unknown;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await connection.DisposeAsync();
        await dataSource.DisposeAsync();
    }
}
