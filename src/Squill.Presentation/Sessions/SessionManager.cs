using System.Collections.ObjectModel;
using Squill.Core;
using Squill.Data;
using Squill.Platform.Profiles;
using Squill.Platform.Ssh;

namespace Squill.Presentation.Sessions;

/// <summary>Opens profiles into <see cref="ConnectionSession"/>s and tracks the open ones.</summary>
/// <param name="providers">Resolves the provider of each profile's dialect.</param>
/// <param name="store">Source of the database password and the SSH secret.</param>
/// <param name="tunnels">Opens the SSH tunnel of profiles that need one.</param>
public sealed class SessionManager(ProviderRegistry providers, IProfileStore store, ISshTunnelFactory tunnels)
{
    /// <summary>Gets the open sessions.</summary>
    public ObservableCollection<ConnectionSession> Sessions { get; } = [];

    /// <summary>Opens the tunnel (when configured), the connection and reads the schema.</summary>
    /// <param name="profile">Profile to open.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new session, already added to <see cref="Sessions"/>.</returns>
    public async ValueTask<ConnectionSession> OpenAsync(ConnectionProfile profile, CancellationToken ct)
    {
        var provider = providers.Get(profile.Dialect);
        var password = await store.GetPasswordAsync(profile.Id, ct);
        ISshTunnel? tunnel = null;
        IDatabaseConnection? connection = null;
        try
        {
            var effective = profile;
            if (profile.Ssh is { } ssh)
            {
                var secret = await store.GetSshSecretAsync(profile.Id, ct);
                tunnel = await tunnels.OpenAsync(ssh, secret, profile.Host ?? "", profile.Port ?? 0, ct);
                effective = profile with { Host = "127.0.0.1", Port = tunnel.LocalPort };
            }
            connection = await provider.OpenAsync(effective, password, ct);
            var schema = await connection.ReadSchemaAsync(ct);
            var session = new ConnectionSession(profile, connection, schema, tunnel);
            Sessions.Add(session);
            return session;
        }
        catch
        {
            if (connection is not null)
                await connection.DisposeAsync();
            if (tunnel is not null)
                await tunnel.DisposeAsync();
            throw;
        }
    }

    /// <summary>Removes the session and disposes its connection, then its tunnel.</summary>
    /// <param name="session">Session to close.</param>
    public async ValueTask CloseAsync(ConnectionSession session)
    {
        Sessions.Remove(session);
        await session.DisposeAsync();
    }
}
