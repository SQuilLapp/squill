using System.Net;
using System.Net.Sockets;
using Squill.Core;
using Squill.Platform.Proxy;
using Squill.Platform.Ssh;
using Squill.Tests.Containers;
using Xunit;

namespace Squill.Platform.Tests;

/// <summary>The proxy forwarder against real proxy containers in front of a PostgreSQL container (and an SSH server) that have no published port.</summary>
public sealed class ProxyContainerTests(ProxyFixture fixture) : IClassFixture<ProxyFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ValueTask<bool> Accept(SshHostKeyRequest request, CancellationToken ct) => ValueTask.FromResult(true);

    /// <summary>Sends a PostgreSQL SSLRequest through the forward: any server answers one byte, 'S' or 'N'.</summary>
    private static async Task AssertPostgresAnswersAsync(int localPort)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, localPort, Ct);
        var stream = client.GetStream();
        await stream.WriteAsync(new byte[] { 0, 0, 0, 8, 4, 0xD2, 0x16, 0x2F }, Ct);
        var answer = new byte[1];
        Assert.Equal(1, await stream.ReadAsync(answer, Ct));
        Assert.Contains((char)answer[0], "SN");
    }

    private ProxySettings Socks(string? user = ProxyFixture.SocksUser) => new(ProxyKind.Socks5, "127.0.0.1", fixture.SocksPort, user);

    private ProxySettings Http() => new(ProxyKind.HttpConnect, "127.0.0.1", fixture.HttpPort);

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Socks5_with_a_password_reaches_a_database_that_is_not_published()
    {
        await using var tunnel = await new ProxyTunnelFactory().OpenAsync(Socks(), ProxyFixture.SocksPassword, SshJumpFixture.DatabaseAlias, 5432, Ct);
        await AssertPostgresAnswersAsync(tunnel.LocalPort);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Socks5_with_a_wrong_password_is_refused()
    {
        await Assert.ThrowsAsync<IOException>(async () =>
            await new ProxyTunnelFactory().OpenAsync(Socks(), "not-the-password", SshJumpFixture.DatabaseAlias, 5432, Ct));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task HttpConnect_reaches_a_database_that_is_not_published()
    {
        await using var tunnel = await new ProxyTunnelFactory().OpenAsync(Http(), null, SshJumpFixture.DatabaseAlias, 5432, Ct);
        await AssertPostgresAnswersAsync(tunnel.LocalPort);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Ssh_connection_goes_through_the_socks5_proxy()
    {
        var jump = new SshTunnelSettings("jump", 2222, SshJumpHost.UserName, SshAuthMethod.Password, null);
        await using var tunnel = await new SshNetTunnelFactory(new InMemoryKnownHostsStore()).OpenAsync(
            jump, SshJumpHost.Password, Socks(), ProxyFixture.SocksPassword, SshJumpFixture.DatabaseAlias, 5432, Accept, Ct);
        await AssertPostgresAnswersAsync(tunnel.LocalPort);
    }
}
