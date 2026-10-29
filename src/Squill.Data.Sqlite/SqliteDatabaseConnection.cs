using Microsoft.Data.Sqlite;
using Squill.Core;

namespace Squill.Data.Sqlite;

/// <summary>SQLite implementation of <see cref="IDatabaseConnection"/> over one open connection.</summary>
internal sealed class SqliteDatabaseConnection(SqliteConnection connection) : IDatabaseConnection
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
            foreach (var parameter in parameters)
                command.Parameters.AddWithValue("@" + parameter.Name, parameter.Value ?? DBNull.Value);
            var reader = await command.ExecuteReaderAsync(cancellationToken);
            return new SqliteResultReader(command, reader);
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
    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
