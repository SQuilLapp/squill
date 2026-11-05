using Squill.Core;

namespace Squill.Data.Oracle;

/// <summary>An Oracle connection held by a bridge process.</summary>
internal sealed class OracleDatabaseConnection(BridgeClient client, string serverVersion) : IDatabaseConnection
{
    /// <inheritdoc />
    public string ServerVersion { get; } = serverVersion;

    /// <inheritdoc />
    public async ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
    {
        var response = await client.SendAsync(new BridgeRequest(
            0, "execute",
            Sql: sql,
            Parameters: [.. parameters.Select(p => new WireParameter(p.Name, WireValue.Encode(p.Value)))]), cancellationToken);
        return new OracleResultReader(client, response);
    }

    /// <inheritdoc />
    public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) =>
        OracleSchemaReader.ReadAsync(this, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken)
    {
        // Oracle has no cross-database browsing here: the "databases" are the schemas (users) of the connected service.
        await using var reader = await ExecuteAsync($"select username from all_users where username not in {OracleSchemaReader.SystemUsers} and username not like 'APEX%' and username not like 'FLOWS%' order by username", [], cancellationToken);
        return [.. (await reader.ReadAllAsync(256, cancellationToken)).Select(r => (string)r[0]!)];
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => client.DisposeAsync();
}
