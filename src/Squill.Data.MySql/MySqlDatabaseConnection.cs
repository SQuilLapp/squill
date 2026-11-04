using MySqlConnector;
using Squill.Core;

namespace Squill.Data.MySql;

/// <summary>MySQL implementation of <see cref="IDatabaseConnection"/> over one open connection.</summary>
internal sealed class MySqlDatabaseConnection(MySqlConnection connection, int commandTimeoutSeconds) : IDatabaseConnection
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
    public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) =>
        MySqlSchemaReader.ReadAsync(connection, cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
