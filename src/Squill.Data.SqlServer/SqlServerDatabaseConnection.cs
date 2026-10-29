using Microsoft.Data.SqlClient;
using Squill.Core;

namespace Squill.Data.SqlServer;

/// <summary>SQL Server implementation of <see cref="IDatabaseConnection"/> over one open connection.</summary>
internal sealed class SqlServerDatabaseConnection(SqlConnection connection) : IDatabaseConnection
{
    /// <inheritdoc />
    public string ServerVersion => connection.ServerVersion;

    /// <inheritdoc />
    public async ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
    {
        var command = new SqlCommand(sql, connection);
        try
        {
            foreach (var parameter in parameters)
                command.Parameters.Add(new SqlParameter("@" + parameter.Name, parameter.Value ?? DBNull.Value));
            var reader = await command.ExecuteReaderAsync(cancellationToken);
            return new SqlServerResultReader(command, reader);
        }
        catch
        {
            await command.DisposeAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("P1-14");

    /// <inheritdoc />
    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
