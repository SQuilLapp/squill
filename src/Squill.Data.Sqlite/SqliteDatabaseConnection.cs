using Microsoft.Data.Sqlite;
using Squill.Core;

namespace Squill.Data.Sqlite;

/// <summary>SQLite implementation of <see cref="IDatabaseConnection"/> over one open connection.</summary>
internal sealed class SqliteDatabaseConnection(SqliteConnection connection, int commandTimeoutSeconds) : IDatabaseConnection
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
    public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) =>
        SqliteSchemaReader.ReadAsync(connection, cancellationToken);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<string>>([Path.GetFileName(connection.DataSource) is { Length: > 0 } name ? name : "main"]);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
