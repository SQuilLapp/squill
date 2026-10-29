using Microsoft.Data.Sqlite;
using Squill.Core;

namespace Squill.Data.Sqlite;

internal sealed class SqliteDatabaseConnection(SqliteConnection connection) : IDatabaseConnection
{
    public string ServerVersion => connection.ServerVersion;

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

    public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("P1-07");

    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
