using Squill.Core;
using Squill.Data;
using Squill.Platform.Ssh;

namespace Squill.Presentation.Sessions;

/// <summary>A live connection to a database, with its optional SSH tunnel and its last schema snapshot.</summary>
public sealed class ConnectionSession : IAsyncDisposable
{
    private readonly ISshTunnel? _tunnel;

    internal ConnectionSession(ConnectionProfile profile, IDatabaseConnection connection, SchemaSnapshot schema, ISshTunnel? tunnel)
    {
        Profile = profile;
        Connection = connection;
        Schema = schema;
        _tunnel = tunnel;
    }

    /// <summary>Gets the profile the session was opened from (with the original host, not the tunnel's).</summary>
    public ConnectionProfile Profile { get; }

    /// <summary>Gets the open database connection.</summary>
    public IDatabaseConnection Connection { get; }

    /// <summary>Gets the schema as of the last read.</summary>
    public SchemaSnapshot Schema { get; private set; }

    /// <summary>Reads the schema again and replaces <see cref="Schema"/>.</summary>
    /// <param name="ct">Cancellation token.</param>
    public async ValueTask RefreshSchemaAsync(CancellationToken ct) => Schema = await Connection.ReadSchemaAsync(ct);

    /// <summary>Disposes the connection, then the tunnel.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await Connection.DisposeAsync();
        }
        finally
        {
            if (_tunnel is not null)
                await _tunnel.DisposeAsync();
        }
    }
}
