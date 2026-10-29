using Npgsql;
using Squill.Core;

namespace Squill.Data.Postgres;

internal sealed class PostgresDatabaseConnection(NpgsqlDataSource dataSource, NpgsqlConnection connection) : IDatabaseConnection
{
    public string ServerVersion => connection.PostgreSqlVersion.ToString();

    public async ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(sql, connection);
        try
        {
            foreach (var parameter in parameters)
                command.Parameters.Add(new NpgsqlParameter(parameter.Name, parameter.Value ?? DBNull.Value));
            var reader = await command.ExecuteReaderAsync(cancellationToken);
            return new PostgresResultReader(command, reader);
        }
        catch
        {
            await command.DisposeAsync();
            throw;
        }
    }

    public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) =>
        PostgresSchemaReader.ReadAsync(connection, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await connection.DisposeAsync();
        await dataSource.DisposeAsync();
    }
}
