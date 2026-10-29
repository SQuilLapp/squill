using MySqlConnector;
using Squill.Core;

namespace Squill.Data.MySql;

internal sealed class MySqlDatabaseConnection(MySqlConnection connection) : IDatabaseConnection
{
    public string ServerVersion => connection.ServerVersion;

    public async ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
    {
        var command = new MySqlCommand(sql, connection);
        try
        {
            foreach (var parameter in parameters)
                command.Parameters.Add(new MySqlParameter(parameter.Name, parameter.Value ?? DBNull.Value));
            var reader = await command.ExecuteReaderAsync(cancellationToken);
            return new MySqlResultReader(command, reader);
        }
        catch
        {
            await command.DisposeAsync();
            throw;
        }
    }

    public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("P1-12");

    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
