using Renci.SshNet;
using Squill.Core;

namespace Squill.Platform.Ssh;

/// <summary>SSH.NET based <see cref="ISshTunnelFactory"/>.</summary>
/// <param name="store">Remembered host keys.</param>
public sealed class SshNetTunnelFactory(IKnownHostsStore store) : ISshTunnelFactory
{
    /// <summary>What to do with a presented host key.</summary>
    internal enum HostKeyVerdict
    {
        /// <summary>Known and equal, or unknown and accepted.</summary>
        Trusted,

        /// <summary>Known with another fingerprint.</summary>
        Changed,

        /// <summary>Unknown and not accepted, or no prompt to ask.</summary>
        Rejected,
    }

    /// <inheritdoc/>
    public async ValueTask<ISshTunnel> OpenAsync(SshTunnelSettings settings, string? secret, string remoteHost, int remotePort, SshHostKeyPrompt? prompt, CancellationToken ct)
    {
        Validate(settings, remoteHost, remotePort);
        ct.ThrowIfCancellationRequested();

        AuthenticationMethod auth = settings.AuthMethod == SshAuthMethod.Password
            ? new PasswordAuthenticationMethod(settings.UserName, secret ?? string.Empty)
            : new PrivateKeyAuthenticationMethod(settings.UserName, string.IsNullOrEmpty(secret)
                ? new PrivateKeyFile(settings.PrivateKeyPath!)
                : new PrivateKeyFile(settings.PrivateKeyPath!, secret));

        var client = new SshClient(new ConnectionInfo(settings.Host, settings.Port, settings.UserName, auth));
        SshHostKeyRequest? presented = null;
        var verdict = HostKeyVerdict.Trusted;
        client.HostKeyReceived += (_, e) =>
        {
            presented = new SshHostKeyRequest(settings.Host, settings.Port, e.HostKeyName, "SHA256:" + e.FingerPrintSHA256);
            // The event is synchronous and runs on the connect thread (never the UI thread), so blocking on the store and the prompt is safe.
            verdict = DecideAsync(store, presented, prompt, ct).AsTask().GetAwaiter().GetResult();
            e.CanTrust = verdict == HostKeyVerdict.Trusted;
        };
        ForwardedPortLocal? port = null;
        try
        {
            await client.ConnectAsync(ct).ConfigureAwait(false);
            port = new ForwardedPortLocal("127.0.0.1", 0, remoteHost, (uint)remotePort);
            client.AddForwardedPort(port);
            port.Start();
            return new Tunnel(client, port);
        }
        catch (Exception) when (presented is not null && verdict != HostKeyVerdict.Trusted)
        {
            port?.Dispose();
            client.Dispose();
            throw verdict == HostKeyVerdict.Changed
                ? new SshHostKeyChangedException(presented, store.Location, await store.FindAsync(presented.Host, presented.Port, presented.Algorithm, CancellationToken.None).ConfigureAwait(false))
                : new SshHostKeyRejectedException(presented);
        }
        catch
        {
            port?.Dispose();
            client.Dispose();
            throw;
        }
    }

    /// <summary>Applies the trust on first use rules to a presented key.</summary>
    internal static async ValueTask<HostKeyVerdict> DecideAsync(IKnownHostsStore store, SshHostKeyRequest key, SshHostKeyPrompt? prompt, CancellationToken ct)
    {
        var known = await store.FindAsync(key.Host, key.Port, key.Algorithm, ct).ConfigureAwait(false);
        if (known is not null)
        {
            return known == key.Fingerprint ? HostKeyVerdict.Trusted : HostKeyVerdict.Changed;
        }

        if (prompt is null || !await prompt(key, ct).ConfigureAwait(false))
        {
            return HostKeyVerdict.Rejected;
        }

        await store.SaveAsync(key, ct).ConfigureAwait(false);
        return HostKeyVerdict.Trusted;
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
