using Squill.Core;

namespace Squill.Data.Oracle;

/// <summary>
/// Opens Oracle connections through a separate bridge process that hosts the (not Native AOT compatible) Oracle driver; see
/// <c>docs/poc/oracle-bridge.md</c>. Proof of concept: no TLS, no multiple result sets, cancelling a statement kills the connection.
/// </summary>
public sealed class OracleDatabaseProvider : IDatabaseProvider
{
    /// <summary>Service name used when the profile has no database.</summary>
    public const string DefaultService = "FREEPDB1";

    private readonly Func<BridgeClient> _startBridge;

    /// <summary>Creates a provider that starts the bridge executable (see the <c>SQUILL_ORACLE_BRIDGE</c> variable).</summary>
    public OracleDatabaseProvider() : this(BridgeClient.StartProcess)
    {
    }

    internal OracleDatabaseProvider(Func<BridgeClient> startBridge) => _startBridge = startBridge;

    /// <inheritdoc />
    public DatabaseDialect Dialect => DatabaseDialect.Oracle;

    /// <inheritdoc />
    public async ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile profile, string? password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrEmpty(profile.Host))
            throw new ArgumentException("An Oracle profile requires a host.", nameof(profile));

        var client = _startBridge();
        try
        {
            var response = await client.SendAsync(new BridgeRequest(
                0, "open",
                Host: profile.Host,
                Port: profile.Port ?? 1521,
                Service: string.IsNullOrEmpty(profile.Database) ? DefaultService : profile.Database,
                User: profile.UserName,
                Password: password,
                ConnectTimeoutSeconds: profile.ConnectTimeoutSeconds,
                CommandTimeoutSeconds: profile.CommandTimeoutSeconds), cancellationToken);
            return new OracleDatabaseConnection(client, response.ServerVersion ?? "");
        }
        catch
        {
            await client.DisposeAsync();
            throw;
        }
    }
}
