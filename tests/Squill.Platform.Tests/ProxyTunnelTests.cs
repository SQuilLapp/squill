using System.Net;
using System.Net.Sockets;
using System.Text;
using Squill.Core;
using Squill.Platform.Proxy;
using Xunit;

namespace Squill.Platform.Tests;

/// <summary>The proxy forwarder against in-process fake SOCKS5 and HTTP CONNECT proxies in front of an echo server.</summary>
public sealed class ProxyTunnelTests : IAsyncDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly TcpListener _echo = new(IPAddress.Loopback, 0);
    private readonly List<FakeProxy> _proxies = [];

    public ProxyTunnelTests()
    {
        _echo.Start();
        _ = Task.Run(async () =>
        {
            while (true)
            {
                var c = await _echo.AcceptSocketAsync();
                _ = Task.Run(async () =>
                {
                    using var s = new NetworkStream(c, ownsSocket: true);
                    try { await s.CopyToAsync(s); } catch (IOException) { }
                });
            }
        });
    }

    public ValueTask DisposeAsync()
    {
        _echo.Stop();
        foreach (var p in _proxies)
            p.Dispose();
        return ValueTask.CompletedTask;
    }

    private int EchoPort => ((IPEndPoint)_echo.LocalEndpoint).Port;

    private FakeProxy Start(ProxyKind kind, string? user = null, string? password = null, string? refuse = null)
    {
        var p = new FakeProxy(kind, user, password, refuse, EchoPort);
        _proxies.Add(p);
        return p;
    }

    private static async Task<string> EchoAsync(int port, string text)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port, Ct);
        var stream = client.GetStream();
        var bytes = Encoding.ASCII.GetBytes(text);
        await stream.WriteAsync(bytes, Ct);
        var back = new byte[bytes.Length];
        await stream.ReadExactlyAsync(back, Ct);
        return Encoding.ASCII.GetString(back);
    }

    [Theory]
    [InlineData(ProxyKind.Socks5)]
    [InlineData(ProxyKind.HttpConnect)]
    public async Task Forwards_through_an_open_proxy_and_the_proxy_gets_the_host_name(ProxyKind kind)
    {
        var proxy = Start(kind);
        await using var tunnel = await new ProxyTunnelFactory().OpenAsync(new ProxySettings(kind, "127.0.0.1", proxy.Port), null, "db.internal", 5432, Ct);

        Assert.Equal("hello", await EchoAsync(tunnel.LocalPort, "hello"));
        Assert.Equal("db.internal:5432", proxy.LastTarget);
    }

    [Theory]
    [InlineData(ProxyKind.Socks5)]
    [InlineData(ProxyKind.HttpConnect)]
    public async Task Authenticates_with_user_and_password(ProxyKind kind)
    {
        var proxy = Start(kind, "me", "s3cret");
        await using var tunnel = await new ProxyTunnelFactory().OpenAsync(new ProxySettings(kind, "127.0.0.1", proxy.Port, "me"), "s3cret", "db", 5432, Ct);

        Assert.Equal("ok", await EchoAsync(tunnel.LocalPort, "ok"));
    }

    [Theory]
    [InlineData(ProxyKind.Socks5)]
    [InlineData(ProxyKind.HttpConnect)]
    public async Task A_wrong_password_fails_without_leaking_it(ProxyKind kind)
    {
        var proxy = Start(kind, "me", "s3cret");

        var ex = await Assert.ThrowsAsync<IOException>(async () =>
            await new ProxyTunnelFactory().OpenAsync(new ProxySettings(kind, "127.0.0.1", proxy.Port, "me"), "wrong-pw", "db", 5432, Ct));

        Assert.Contains("user name or password", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("wrong-pw", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ProxyKind.Socks5)]
    [InlineData(ProxyKind.HttpConnect)]
    public async Task A_refused_target_fails_with_the_proxy_reason(ProxyKind kind)
    {
        var proxy = Start(kind, refuse: "deny");

        var ex = await Assert.ThrowsAsync<IOException>(async () =>
            await new ProxyTunnelFactory().OpenAsync(new ProxySettings(kind, "127.0.0.1", proxy.Port), null, "db", 5432, Ct));

        Assert.Contains("refused the", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_proxy_that_requires_auth_when_none_is_configured_fails()
    {
        var proxy = Start(ProxyKind.Socks5, "me", "pw");

        await Assert.ThrowsAsync<IOException>(async () =>
            await new ProxyTunnelFactory().OpenAsync(new ProxySettings(ProxyKind.Socks5, "127.0.0.1", proxy.Port), null, "db", 5432, Ct));
    }

    [Theory]
    [InlineData("", 1, "h", 1)]
    [InlineData("p", 0, "h", 1)]
    [InlineData("p", 1, " ", 1)]
    [InlineData("p", 1, "h", 70000)]
    public async Task Rejects_invalid_arguments_before_connecting(string proxyHost, int proxyPort, string host, int port) =>
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await new ProxyTunnelFactory().OpenAsync(new ProxySettings(ProxyKind.Socks5, proxyHost, proxyPort), null, host, port, Ct));

    [Fact]
    public async Task An_unreachable_proxy_fails()
    {
        var free = new TcpListener(IPAddress.Loopback, 0);
        free.Start();
        var port = ((IPEndPoint)free.LocalEndpoint).Port;
        free.Stop();

        await Assert.ThrowsAnyAsync<SocketException>(async () =>
            await new ProxyTunnelFactory().OpenAsync(new ProxySettings(ProxyKind.Socks5, "127.0.0.1", port), null, "db", 5432, Ct));
    }

    [Theory]
    [InlineData(ProxyKind.Socks5)]
    [InlineData(ProxyKind.HttpConnect)]
    public async Task A_hundred_connections_leave_no_socket_open(ProxyKind kind)
    {
        var proxy = Start(kind);
        await using var tunnel = (ProxyTunnel)await new ProxyTunnelFactory().OpenAsync(new ProxySettings(kind, "127.0.0.1", proxy.Port), null, "db", 5432, Ct);

        for (var i = 0; i < 100; i++)
            Assert.Equal("x" + i, await EchoAsync(tunnel.LocalPort, "x" + i));

        for (var i = 0; i < 100 && tunnel.LiveSockets > 0; i++)
            await Task.Delay(50, Ct);
        Assert.Equal(0, tunnel.LiveSockets);
    }

    [Fact]
    public async Task Disposing_the_tunnel_closes_the_forwarded_connections_and_the_listener()
    {
        var proxy = Start(ProxyKind.Socks5);
        var tunnel = (ProxyTunnel)await new ProxyTunnelFactory().OpenAsync(new ProxySettings(ProxyKind.Socks5, "127.0.0.1", proxy.Port), null, "db", 5432, Ct);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, tunnel.LocalPort, Ct);
        var stream = client.GetStream();
        await stream.WriteAsync("a"u8.ToArray(), Ct);
        await stream.ReadExactlyAsync(new byte[1], Ct);
        var port = tunnel.LocalPort;

        await tunnel.DisposeAsync();

        Assert.Equal(0, await stream.ReadAsync(new byte[1], Ct));
        using var late = new TcpClient();
        await Assert.ThrowsAnyAsync<SocketException>(async () => await late.ConnectAsync(IPAddress.Loopback, port, Ct));
    }

    /// <summary>Minimal SOCKS5 / HTTP CONNECT proxy that always connects to the echo server, whatever the requested target.</summary>
    private sealed class FakeProxy : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly ProxyKind _kind;
        private readonly string? _user;
        private readonly string? _password;
        private readonly string? _refuse;
        private readonly int _echoPort;

        public FakeProxy(ProxyKind kind, string? user, string? password, string? refuse, int echoPort)
        {
            (_kind, _user, _password, _refuse, _echoPort) = (kind, user, password, refuse, echoPort);
            _listener.Start();
            _ = Task.Run(AcceptAsync);
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public string? LastTarget { get; private set; }

        public void Dispose() => _listener.Stop();

        private async Task AcceptAsync()
        {
            try
            {
                while (true)
                {
                    var c = await _listener.AcceptSocketAsync();
                    _ = Task.Run(() => ServeAsync(c));
                }
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
            {
            }
        }

        private async Task ServeAsync(Socket client)
        {
            using var c = new NetworkStream(client, ownsSocket: true);
            try
            {
                if (!await (_kind == ProxyKind.Socks5 ? HandshakeSocks5Async(c) : HandshakeHttpAsync(c)))
                    return;
                using var upstream = new TcpClient();
                await upstream.ConnectAsync(IPAddress.Loopback, _echoPort);
                var u = upstream.GetStream();
                await Task.WhenAny(c.CopyToAsync(u), u.CopyToAsync(c));
            }
            catch (IOException)
            {
            }
        }

        private static async Task<byte[]> Read(NetworkStream s, int n)
        {
            var b = new byte[n];
            await s.ReadExactlyAsync(b);
            return b;
        }

        private async Task<bool> HandshakeSocks5Async(NetworkStream c)
        {
            var head = await Read(c, 2);
            var methods = await Read(c, head[1]);
            if (_user is null)
            {
                await c.WriteAsync(new byte[] { 5, 0 });
            }
            else
            {
                if (!methods.Contains((byte)2))
                {
                    await c.WriteAsync(new byte[] { 5, 0xFF });
                    return false;
                }

                await c.WriteAsync(new byte[] { 5, 2 });
                await Read(c, 1);
                var user = Encoding.UTF8.GetString(await Read(c, (await Read(c, 1))[0]));
                var pass = Encoding.UTF8.GetString(await Read(c, (await Read(c, 1))[0]));
                var ok = user == _user && pass == _password;
                await c.WriteAsync(new byte[] { 1, (byte)(ok ? 0 : 1) });
                if (!ok)
                    return false;
            }

            var req = await Read(c, 4);
            string host;
            if (req[3] == 3)
            {
                host = Encoding.UTF8.GetString(await Read(c, (await Read(c, 1))[0]));
            }
            else
            {
                host = new IPAddress(await Read(c, req[3] == 1 ? 4 : 16)).ToString();
            }

            var port = await Read(c, 2);
            LastTarget = host + ":" + ((port[0] << 8) | port[1]);
            await c.WriteAsync(new byte[] { 5, (byte)(_refuse is null ? 0 : 2), 0, 1, 0, 0, 0, 0, 0, 0 });
            return _refuse is null;
        }

        private async Task<bool> HandshakeHttpAsync(NetworkStream c)
        {
            var text = new StringBuilder();
            var one = new byte[1];
            while (!text.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
            {
                await c.ReadExactlyAsync(one);
                text.Append((char)one[0]);
            }

            var lines = text.ToString().Split("\r\n");
            LastTarget = lines[0].Split(' ')[1];
            var status = "200 Connection established";
            if (_refuse is not null)
            {
                status = "403 Forbidden";
            }
            else if (_user is not null)
            {
                var expected = "Proxy-Authorization: Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(_user + ":" + _password));
                if (!lines.Contains(expected))
                    status = "407 Proxy Authentication Required";
            }

            await c.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Length: 0\r\n\r\n"));
            return status[0] == '2';
        }
    }
}
