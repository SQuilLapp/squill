using Squill.Core;

namespace Squill.Platform.Ssh;

/// <summary>Opens SSH tunnels to databases that are only reachable through a jump host.</summary>
public interface ISshTunnelFactory
{
    /// <summary>Connects to the SSH server and forwards a loopback port to the remote endpoint.</summary>
    /// <param name="settings">SSH server and account settings.</param>
    /// <param name="secret">Password, or private key passphrase (may be <see langword="null"/>).</param>
    /// <param name="remoteHost">Host to reach, as seen from the SSH server.</param>
    /// <param name="remotePort">Port to reach on <paramref name="remoteHost"/>.</param>
    /// <param name="prompt">Asks whether an unknown host key can be trusted; <see langword="null"/> refuses every unknown key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The open tunnel.</returns>
    /// <exception cref="ArgumentException">A host is empty, a port is outside 1-65535, or the key path is missing for key authentication.</exception>
    ValueTask<ISshTunnel> OpenAsync(SshTunnelSettings settings, string? secret, string remoteHost, int remotePort, SshHostKeyPrompt? prompt, CancellationToken ct);

    /// <summary>Same as <see cref="OpenAsync(SshTunnelSettings, string?, string, int, SshHostKeyPrompt?, CancellationToken)"/>, with the SSH connection itself carried by a SOCKS5 or HTTP CONNECT proxy.</summary>
    /// <param name="settings">SSH server and account settings.</param>
    /// <param name="secret">Password, or private key passphrase (may be <see langword="null"/>).</param>
    /// <param name="proxy">Proxy the SSH connection goes through.</param>
    /// <param name="proxySecret">Proxy password (may be <see langword="null"/>).</param>
    /// <param name="remoteHost">Host to reach, as seen from the SSH server.</param>
    /// <param name="remotePort">Port to reach on <paramref name="remoteHost"/>.</param>
    /// <param name="prompt">Asks whether an unknown host key can be trusted; <see langword="null"/> refuses every unknown key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The open tunnel.</returns>
    /// <exception cref="NotSupportedException">The factory cannot go through a proxy.</exception>
    ValueTask<ISshTunnel> OpenAsync(SshTunnelSettings settings, string? secret, ProxySettings proxy, string? proxySecret, string remoteHost, int remotePort, SshHostKeyPrompt? prompt, CancellationToken ct) =>
        throw new NotSupportedException();
}
