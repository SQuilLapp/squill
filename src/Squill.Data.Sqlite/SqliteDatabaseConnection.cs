using System.Globalization;
using Microsoft.Data.Sqlite;
using Squill.Core;
using Squill.Core.Plans;
using Squill.Core.Transactions;
using SQLitePCL;

namespace Squill.Data.Sqlite;

/// <summary>SQLite implementation of <see cref="IDatabaseConnection"/> over one open connection.</summary>
internal sealed class SqliteDatabaseConnection(SqliteConnection connection, int commandTimeoutSeconds) : IDatabaseConnection, IPlanReader, ITransactionProbe
{
    /// <inheritdoc />
    public string ServerVersion => connection.ServerVersion;

    /// <inheritdoc />
    public async ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        try
        {
            command.CommandText = sql;
            command.CommandTimeout = commandTimeoutSeconds;
            foreach (var parameter in parameters)
                command.Parameters.AddWithValue("@" + parameter.Name, parameter.Value ?? DBNull.Value);
            var reader = await SqliteInterrupt.RunAsync(connection, commandTimeoutSeconds, async () => await command.ExecuteReaderAsync(cancellationToken), cancellationToken);
            return new SqliteResultReader(connection, commandTimeoutSeconds, command, reader);
        }
        catch
        {
            await command.DisposeAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public bool SupportsActualPlan => false;

    /// <inheritdoc />
    public async ValueTask<QueryPlan> ExplainAsync(string sql, IReadOnlyList<QueryParameter> parameters, PlanMode mode, CancellationToken cancellationToken)
    {
        if (mode == PlanMode.Actual)
            throw new PlanUnavailableException("engine", "SQLite cannot report actual execution statistics.");
        await using var reader = await ExecuteAsync("EXPLAIN QUERY PLAN " + sql, parameters, cancellationToken);
        var rows = new List<SqlitePlanRow>();
        while (await reader.ReadBatchAsync(256, cancellationToken) is { } batch)
        {
            foreach (var row in batch)
                rows.Add(new SqlitePlanRow(Convert.ToInt64(row[0], CultureInfo.InvariantCulture), Convert.ToInt64(row[1], CultureInfo.InvariantCulture), Convert.ToString(row[3], CultureInfo.InvariantCulture) ?? ""));
        }
        return SqlitePlanParser.Parse(sql, rows);
    }

    /// <inheritdoc />
    public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) =>
        SqliteSchemaReader.ReadAsync(connection, cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<string>>([Path.GetFileName(connection.DataSource) is { Length: > 0 } name ? name : "main"]);

    /// <inheritdoc />
    public ValueTask<TransactionProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            // Autocommit mode means no transaction is open; SQLite has no aborted state.
            return ValueTask.FromResult(connection.Handle is { } handle
                ? new TransactionProbeResult(raw.sqlite3_get_autocommit(handle) == 0, false)
                : TransactionProbeResult.Unknown);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ValueTask.FromResult(TransactionProbeResult.Unknown);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // The Microsoft.Data.Sqlite pool keeps the connection (and its write lock) with an open transaction otherwise.
        try
        {
            if (connection.Handle is { } handle && raw.sqlite3_get_autocommit(handle) == 0)
            {
                await using var rollback = connection.CreateCommand();
                rollback.CommandText = "ROLLBACK";
                await rollback.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex) when (ex is SqliteException or InvalidOperationException)
        {
            // Closing the connection below is what matters.
        }

        await connection.DisposeAsync();
    }
}
