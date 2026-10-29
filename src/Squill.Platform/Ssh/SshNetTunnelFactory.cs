using Renci.SshNet;
using Squill.Core;

namespace Squill.Platform.Ssh;

/// <summary>SSH.NET based <see cref="ISshTunnelFactory"/>.</summary>
public sealed class SshNetTunnelFactory : ISshTunnelFactory
{
    /// <inheritdoc/>
    public async ValueTask<ISshTunnel> OpenAsync(SshTunnelSettings settings, string? secret, string remoteHost, int remotePort, CancellationToken ct)
    {
        Validate(settings, remoteHost, remotePort);
        ct.ThrowIfCancellationRequested();

        AuthenticationMethod auth = settings.AuthMethod == SshAuthMethod.Password
            ? new PasswordAuthenticationMethod(settings.UserName, secret ?? string.Empty)
            : new PrivateKeyAuthenticationMethod(settings.UserName, string.IsNullOrEmpty(secret)
                ? new PrivateKeyFile(settings.PrivateKeyPath!)
                : new PrivateKeyFile(settings.PrivateKeyPath!, secret));

        var client = new SshClient(new ConnectionInfo(settings.Host, settings.Port, settings.UserName, auth));
        // shortcut: first host key is accepted without verification and not persisted, add known-hosts handling later.
        client.HostKeyReceived += static (_, e) => e.CanTrust = true;
        ForwardedPortLocal? port = null;
        try
        {
            await client.ConnectAsync(ct).ConfigureAwait(false);
            port = new ForwardedPortLocal("127.0.0.1", 0, remoteHost, (uint)remotePort);
            client.AddForwardedPort(port);
            port.Start();
            return new Tunnel(client, port);
        }
        catch
        {
            port?.Dispose();
            client.Dispose();
            throw;
        }
    }

    private static void Validate(SshTunnelSettings settings, string remoteHost, int remotePort)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(settings.Host))
            throw new ArgumentException("SSH host is empty.", nameof(settings));
        if (settings.Port is < 1 or > 65535)
            throw new ArgumentException("SSH port must be between 1 and 65535.", nameof(settings));
        if (settings.AuthMethod == SshAuthMethod.PrivateKey && string.IsNullOrWhiteSpace(settings.PrivateKeyPath))
            throw new ArgumentException("A private key path is required for key authentication.", nameof(settings));
        if (string.IsNullOrWhiteSpace(remoteHost))
            throw new ArgumentException("Remote host is empty.", nameof(remoteHost));
        if (remotePort is < 1 or > 65535)
            throw new ArgumentException("Remote port must be between 1 and 65535.", nameof(remotePort));
    }

    /// <summary>Open forward plus the SSH client that carries it.</summary>
    private sealed class Tunnel(SshClient client, ForwardedPortLocal port) : ISshTunnel
    {
        /// <inheritdoc/>
        public int LocalPort { get; } = (int)port.BoundPort;

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            port.Stop();
            port.Dispose();
            client.Disconnect();
            client.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
