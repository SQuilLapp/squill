using System.Net;
using System.Net.Sockets;
using Squill.Core;
using Squill.Platform.Ssh;
using Squill.Tests.Containers;
using Xunit;

namespace Squill.Platform.Tests;

/// <summary>The SSH tunnel against a real OpenSSH server in front of a PostgreSQL container that has no published port.</summary>
public sealed class SshJumpHostTests(SshJumpFixture fixture) : IClassFixture<SshJumpFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private SshTunnelSettings Password() =>
        new(fixture.Jump.Host, fixture.Jump.Port, SshJumpHost.UserName, SshAuthMethod.Password, null);

    private SshTunnelSettings Key(string path) =>
        new(fixture.Jump.Host, fixture.Jump.Port, SshJumpHost.UserName, SshAuthMethod.PrivateKey, path);

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

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Password_reaches_a_database_that_is_not_published()
    {
        await using var tunnel = await new SshNetTunnelFactory().OpenAsync(Password(), SshJumpHost.Password, SshJumpFixture.DatabaseAlias, 5432, Ct);
        await AssertPostgresAnswersAsync(tunnel.LocalPort);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Key_without_passphrase_reaches_the_database()
    {
        await using var tunnel = await new SshNetTunnelFactory().OpenAsync(Key(fixture.Jump.PlainKeyPath), null, SshJumpFixture.DatabaseAlias, 5432, Ct);
        await AssertPostgresAnswersAsync(tunnel.LocalPort);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Key_with_passphrase_reaches_the_database()
    {
        await using var tunnel = await new SshNetTunnelFactory().OpenAsync(Key(fixture.Jump.EncryptedKeyPath), SshJumpHost.Passphrase, SshJumpFixture.DatabaseAlias, 5432, Ct);
        await AssertPostgresAnswersAsync(tunnel.LocalPort);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Wrong_password_fails_with_the_ssh_message()
    {
        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await new SshNetTunnelFactory().OpenAsync(Password(), "not-the-password", SshJumpFixture.DatabaseAlias, 5432, Ct));
        Assert.Contains("Permission denied", ex.Message, StringComparison.Ordinal);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Wrong_passphrase_fails()
    {
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await new SshNetTunnelFactory().OpenAsync(Key(fixture.Jump.EncryptedKeyPath), "wrong", SshJumpFixture.DatabaseAlias, 5432, Ct));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task DisposeAsync_frees_the_local_port()
    {
        var tunnel = await new SshNetTunnelFactory().OpenAsync(Password(), SshJumpHost.Password, SshJumpFixture.DatabaseAlias, 5432, Ct);
        var port = tunnel.LocalPort;
        await tunnel.DisposeAsync();

        using var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start(); // throws SocketException when the forward still owns the port
    }
}
