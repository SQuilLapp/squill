using System.Globalization;
using Microsoft.Data.Sqlite;
using Squill.Core;
using Squill.Core.Plans;

namespace Squill.Data.Sqlite;

/// <summary>SQLite implementation of <see cref="IDatabaseConnection"/> over one open connection.</summary>
internal sealed class SqliteDatabaseConnection(SqliteConnection connection, int commandTimeoutSeconds) : IDatabaseConnection, IPlanReader
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
    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
