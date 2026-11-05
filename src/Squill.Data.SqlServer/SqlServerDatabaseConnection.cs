using Microsoft.Data.SqlClient;
using Squill.Core;

namespace Squill.Data.SqlServer;

/// <summary>SQL Server implementation of <see cref="IDatabaseConnection"/> over one open connection.</summary>
internal sealed class SqlServerDatabaseConnection(SqlConnection connection, int commandTimeoutSeconds) : IDatabaseConnection
{
    /// <inheritdoc />
    public string ServerVersion => connection.ServerVersion;

    /// <inheritdoc />
    public async ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
    {
        var command = new SqlCommand(sql, connection) { CommandTimeout = commandTimeoutSeconds };
        var messages = new List<string>();
        SqlInfoMessageEventHandler handler = (_, a) => { foreach (SqlError e in a.Errors) messages.Add(e.Message); };
        connection.InfoMessage += handler;
        try
        {
            foreach (var parameter in parameters)
                command.Parameters.Add(new SqlParameter("@" + parameter.Name, parameter.Value ?? DBNull.Value));
            var reader = await command.ExecuteReaderAsync(cancellationToken);
            return new SqlServerResultReader(command, reader, messages, () => connection.InfoMessage -= handler);
        }
        catch (SqlException ex) when (cancellationToken.IsCancellationRequested)
        {
            // SqlClient reports a cancelled command as a SqlException; callers expect the standard cancellation exception.
            connection.InfoMessage -= handler;
            await command.DisposeAsync();
            throw new OperationCanceledException(ex.Message, ex, cancellationToken);
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
        SqlServerSchemaReader.ReadAsync(connection, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken)
    {
        await using var reader = await ExecuteAsync("select name from sys.databases where state = 0 and name not in ('master','tempdb','model','msdb') order by 1", [], cancellationToken);
        return [.. (await reader.ReadAllAsync(256, cancellationToken)).Select(r => (string)r[0]!)];
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
