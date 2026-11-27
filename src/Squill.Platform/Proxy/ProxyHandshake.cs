using System.Net;
using System.Net.Sockets;
using System.Text;
using Squill.Core;

namespace Squill.Platform.Proxy;

/// <summary>Opens a socket to a proxy and asks it for a tunnel to a target: SOCKS5 (RFC 1928, 1929) or HTTP CONNECT with Basic authentication.</summary>
internal static class ProxyHandshake
{
    private const int MaxHeaderBytes = 16 * 1024;

    /// <summary>Connects to the proxy and negotiates a tunnel; the proxy resolves the target host name.</summary>
    /// <param name="proxy">Proxy to use.</param>
    /// <param name="password">Proxy password, or <see langword="null"/>.</param>
    /// <param name="host">Target host, sent by name when it is not an IP address.</param>
    /// <param name="port">Target port.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A socket whose bytes now flow to the target; the caller owns it.</returns>
    /// <exception cref="IOException">The proxy refused the credentials or the target, or spoke something unexpected.</exception>
    public static async ValueTask<Socket> ConnectAsync(ProxySettings proxy, string? password, string host, int port, CancellationToken ct)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(proxy.Host, proxy.Port, ct).ConfigureAwait(false);
            using var stream = new NetworkStream(socket, ownsSocket: false);
            if (proxy.Kind == ProxyKind.Socks5)
                await Socks5Async(stream, proxy.UserName, password, host, port, ct).ConfigureAwait(false);
            else
                await HttpConnectAsync(stream, proxy.UserName, password, host, port, ct).ConfigureAwait(false);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static async ValueTask Socks5Async(NetworkStream s, string? user, string? password, string host, int port, CancellationToken ct)
    {
        var auth = !string.IsNullOrEmpty(user);
        await s.WriteAsync(auth ? new byte[] { 5, 2, 0, 2 } : new byte[] { 5, 1, 0 }, ct).ConfigureAwait(false);
        var choice = await ReadAsync(s, 2, ct).ConfigureAwait(false);
        if (choice[0] != 5)
            throw new IOException("The proxy does not speak SOCKS5.");
        if (choice[1] == 0xFF)
            throw new IOException("The SOCKS5 proxy accepts none of the offered authentication methods.");
        if (choice[1] == 2)
        {
            if (!auth)
                throw new IOException("The SOCKS5 proxy requires authentication.");
            var u = Encoding.UTF8.GetBytes(user!);
            var p = Encoding.UTF8.GetBytes(password ?? "");
            if (u.Length > 255 || p.Length > 255)
                throw new IOException("A SOCKS5 user name or password is limited to 255 bytes.");
            await s.WriteAsync(new byte[] { 1, (byte)u.Length }.Concat(u).Append((byte)p.Length).Concat(p).ToArray(), ct).ConfigureAwait(false);
            if ((await ReadAsync(s, 2, ct).ConfigureAwait(false))[1] != 0)
                throw new IOException("The SOCKS5 proxy refused the user name or password.");
        }
        else if (choice[1] != 0)
        {
            throw new IOException("The SOCKS5 proxy chose an unsupported authentication method.");
        }

        List<byte> request = [5, 1, 0];
        if (IPAddress.TryParse(host, out var ip))
        {
            request.Add(ip.AddressFamily == AddressFamily.InterNetwork ? (byte)1 : (byte)4);
            request.AddRange(ip.GetAddressBytes());
        }
        else
        {
            var name = Encoding.UTF8.GetBytes(host);
            if (name.Length is 0 or > 255)
                throw new IOException("A SOCKS5 target host name is limited to 255 bytes.");
            request.Add(3);
            request.Add((byte)name.Length);
            request.AddRange(name);
        }

        request.Add((byte)(port >> 8));
        request.Add((byte)port);
        await s.WriteAsync(request.ToArray(), ct).ConfigureAwait(false);
        var reply = await ReadAsync(s, 4, ct).ConfigureAwait(false);
        if (reply[1] != 0)
            throw new IOException("The SOCKS5 proxy refused the target: " + Socks5Reason(reply[1]) + ".");
        var rest = reply[3] switch
        {
            1 => 4,
            4 => 16,
            3 => (await ReadAsync(s, 1, ct).ConfigureAwait(false))[0],
            _ => throw new IOException("The SOCKS5 proxy sent an unknown address type."),
        };
        await ReadAsync(s, rest + 2, ct).ConfigureAwait(false);
    }

    private static string Socks5Reason(byte code) => code switch
    {
        1 => "general failure",
        2 => "not allowed by the proxy rules",
        3 => "network unreachable",
        4 => "host unreachable",
        5 => "connection refused",
        6 => "TTL expired",
        7 => "command not supported",
        8 => "address type not supported",
        _ => "error " + code,
    };

    private static async ValueTask HttpConnectAsync(NetworkStream s, string? user, string? password, string host, int port, CancellationToken ct)
    {
        var target = (host.Contains(':', StringComparison.Ordinal) ? "[" + host + "]" : host) + ":" + port;
        var text = $"CONNECT {target} HTTP/1.1\r\nHost: {target}\r\n";
        if (!string.IsNullOrEmpty(user))
            text += "Proxy-Authorization: Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(user + ":" + password)) + "\r\n";
        await s.WriteAsync(Encoding.ASCII.GetBytes(text + "\r\n"), ct).ConfigureAwait(false);

        // Read byte by byte up to the blank line: anything after it already belongs to the tunnel.
        var head = new StringBuilder();
        var one = new byte[1];
        while (!EndsWithBlankLine(head))
        {
            if (head.Length > MaxHeaderBytes || await s.ReadAsync(one, ct).ConfigureAwait(false) == 0)
                throw new IOException("The HTTP proxy closed the connection or sent an oversized answer.");
            head.Append((char)one[0]);
        }

        var all = head.ToString();
        var line = all[..all.IndexOf("\r\n", StringComparison.Ordinal)];
        var parts = line.Split(' ', 3);
        if (parts.Length < 2 || !parts[0].StartsWith("HTTP/", StringComparison.Ordinal) || !int.TryParse(parts[1], out var status))
            throw new IOException("The proxy does not speak HTTP.");
        if (status is < 200 or > 299)
            throw new IOException(status == 407
                ? "The HTTP proxy refused the user name or password (407)."
                : $"The HTTP proxy refused the tunnel: {line}.");
    }

    private static bool EndsWithBlankLine(StringBuilder b) =>
        b.Length >= 4 && b[^4] == '\r' && b[^3] == '\n' && b[^2] == '\r' && b[^1] == '\n';

    private static async ValueTask<byte[]> ReadAsync(NetworkStream s, int count, CancellationToken ct)
    {
        var buffer = new byte[count];
        try
        {
            await s.ReadExactlyAsync(buffer, ct).ConfigureAwait(false);
        }
        catch (EndOfStreamException)
        {
            throw new IOException("The proxy closed the connection during the handshake.");
        }

        return buffer;
    }
}
