using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Squill.Core;
using Squill.Platform.Ssh;

namespace Squill.Platform.Proxy;

/// <summary>Opens loopback listeners that forward each connection to a database through a SOCKS5 or HTTP CONNECT proxy.</summary>
public interface IProxyTunnelFactory
{
    /// <summary>Checks the proxy (one handshake, kept for the first connection) and starts the loopback listener.</summary>
    /// <param name="proxy">Proxy to go through.</param>
    /// <param name="secret">Proxy password (may be <see langword="null"/>); never logged.</param>
    /// <param name="remoteHost">Host to reach; the proxy resolves it.</param>
    /// <param name="remotePort">Port to reach on <paramref name="remoteHost"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The open tunnel; disposing it stops the listener and closes every forwarded connection.</returns>
    /// <exception cref="ArgumentException">A host is empty or a port is outside 1-65535.</exception>
    /// <exception cref="IOException">The proxy is unreachable or refused the credentials or the target.</exception>
    ValueTask<ISshTunnel> OpenAsync(ProxySettings proxy, string? secret, string remoteHost, int remotePort, CancellationToken ct);
}

/// <summary>Socket based <see cref="IProxyTunnelFactory"/>.</summary>
public sealed class ProxyTunnelFactory : IProxyTunnelFactory
{
    /// <inheritdoc/>
    public async ValueTask<ISshTunnel> OpenAsync(ProxySettings proxy, string? secret, string remoteHost, int remotePort, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(proxy);
        if (string.IsNullOrWhiteSpace(proxy.Host))
            throw new ArgumentException("Proxy host is empty.", nameof(proxy));
        if (proxy.Port is < 1 or > 65535)
            throw new ArgumentException("Proxy port must be between 1 and 65535.", nameof(proxy));
        if (string.IsNullOrWhiteSpace(remoteHost))
            throw new ArgumentException("Remote host is empty.", nameof(remoteHost));
        if (remotePort is < 1 or > 65535)
            throw new ArgumentException("Remote port must be between 1 and 65535.", nameof(remotePort));

        var first = await ProxyHandshake.ConnectAsync(proxy, secret, remoteHost, remotePort, ct).ConfigureAwait(false);
        return new ProxyTunnel(proxy, secret, remoteHost, remotePort, first);
    }
}

/// <summary>Loopback listener plus the live forwarded pairs.</summary>
internal sealed class ProxyTunnel : ISshTunnel
{
    private readonly ProxySettings _proxy;
    private readonly string? _secret;
    private readonly string _host;
    private readonly int _port;
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentDictionary<Socket, byte> _live = new();
    private readonly Task _acceptLoop;
    private Socket? _first;

    public ProxyTunnel(ProxySettings proxy, string? secret, string host, int port, Socket first)
    {
        (_proxy, _secret, _host, _port, _first) = (proxy, secret, host, port, first);
        _listener.Start();
        LocalPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    /// <inheritdoc/>
    public int LocalPort { get; }

    /// <summary>Gets the number of sockets (both sides of every pair) still open.</summary>
    public int LiveSockets => _live.Count;

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var client = await _listener.AcceptSocketAsync(_stop.Token).ConfigureAwait(false);
                _ = ForwardAsync(client);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException)
        {
            // Listener stopped.
        }
    }

    private async Task ForwardAsync(Socket client)
    {
        Socket? upstream = null;
        try
        {
            client.NoDelay = true;
            _live[client] = 0;
            upstream = Interlocked.Exchange(ref _first, null)
                ?? await ProxyHandshake.ConnectAsync(_proxy, _secret, _host, _port, _stop.Token).ConfigureAwait(false);
            _live[upstream] = 0;
            using var a = new NetworkStream(client, ownsSocket: false);
            using var b = new NetworkStream(upstream, ownsSocket: false);
            await Task.WhenAny(a.CopyToAsync(b, _stop.Token), b.CopyToAsync(a, _stop.Token)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
            // A refused handshake or a reset closes the pair; the database driver sees the closed socket.
        }
        finally
        {
            Release(client);
            if (upstream is not null)
                Release(upstream);
        }
    }

    private void Release(Socket socket)
    {
        _live.TryRemove(socket, out _);
        socket.Dispose();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        _listener.Stop();
        Interlocked.Exchange(ref _first, null)?.Dispose();
        foreach (var socket in _live.Keys)
            socket.Dispose();
        await _acceptLoop.ConfigureAwait(false);
        _stop.Dispose();
    }
}
