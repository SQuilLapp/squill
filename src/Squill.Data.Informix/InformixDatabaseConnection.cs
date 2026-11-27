using Squill.Core;
using Squill.Core.Transactions;

namespace Squill.Data.Informix;

/// <summary>Informix implementation of <see cref="IDatabaseConnection"/> over one ODBC connection to IBM's CLI driver.</summary>
internal sealed class InformixDatabaseConnection : IDatabaseConnection, ITransactionProbe
{
    private static readonly string[] SystemDatabases = ["sysmaster", "sysutils", "sysuser", "sysadmin", "sysha", "syscdcv1"];

    private readonly OdbcConnection _connection;

    private InformixDatabaseConnection(OdbcConnection connection, string serverVersion)
    {
        _connection = connection;
        ServerVersion = serverVersion;
    }

    /// <summary>Opens a connection and reads the server version.</summary>
    /// <param name="connectionString">A string from <see cref="OdbcConnectionString.Build"/>.</param>
    /// <param name="cancellationToken">Cancels before the connect starts.</param>
    /// <returns>The open connection.</returns>
    /// <exception cref="OdbcException">The driver refused the connection.</exception>
    public static async Task<InformixDatabaseConnection> OpenAsync(string connectionString, CancellationToken cancellationToken)
    {
        var odbc = await OdbcConnection.OpenAsync(connectionString, cancellationToken);
        try
        {
            return new InformixDatabaseConnection(odbc, await ReadVersionAsync(odbc, cancellationToken));
        }
        catch
        {
            odbc.Dispose();
            throw;
        }
    }

    // Not the DRDA handshake version, which says 12.10.0000 whatever the real server is: it is for display only, never for feature checks.
    private static async Task<string> ReadVersionAsync(OdbcConnection odbc, CancellationToken cancellationToken)
    {
        try
        {
            using var reader = await odbc.ExecuteAsync("select first 1 dbinfo('version', 'full') from systables", cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? reader.GetValue(0)?.ToString()?.Trim() ?? "" : "";
        }
        catch (OdbcException)
        {
            return "";
        }
    }

    /// <inheritdoc />
    public string ServerVersion { get; }

    /// <inheritdoc />
    public async ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
    {
        try
        {
            if (parameters.Count == 0 && InformixTransactionStatement.TryParse(sql) is { } op)
            {
                await (op switch { InformixTransactionStatement.Begin => _connection.BeginAsync(cancellationToken), InformixTransactionStatement.Commit => _connection.CommitAsync(cancellationToken), _ => _connection.RollbackAsync(cancellationToken) });
                return new NoResultReader();
            }

            var (text, names) = PositionalParameters.Rewrite(sql);
            var values = new object?[names.Count];
            for (var i = 0; i < values.Length; i++)
            {
                var parameter = parameters.FirstOrDefault(p => p.Name == names[i])
                    ?? throw new ArgumentException($"No value was given for the parameter @{names[i]}.", nameof(parameters));
                values[i] = parameter.Value;
            }

            return new InformixResultReader(await _connection.ExecuteAsync(text, values, cancellationToken));
        }
        catch (OdbcException ex) when (cancellationToken.IsCancellationRequested)
        {
            // The driver reports a cancelled statement as a diagnostic; callers expect the standard cancellation exception.
            throw new OperationCanceledException(ex.Message, ex, cancellationToken);
        }
    }

    /// <inheritdoc />
    public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) =>
        InformixSchemaReader.ReadAsync(this, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken)
    {
        await using var reader = await ExecuteAsync("select trim(name) from sysmaster:sysdatabases order by 1", [], cancellationToken);
        return [.. (await reader.ReadAllAsync(256, cancellationToken)).Select(r => (string)r[0]!).Where(n => !SystemDatabases.Contains(n, StringComparer.OrdinalIgnoreCase))];
    }

    /// <inheritdoc />
    /// <remarks>Autocommit is off exactly while a transaction is open, so the answer is the connection's own state; no round trip.</remarks>
    public ValueTask<TransactionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(new TransactionProbeResult(_connection.InTransaction, false));

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _connection.Dispose();
        return ValueTask.CompletedTask;
    }
}
