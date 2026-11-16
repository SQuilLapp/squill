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

    private static ValueTask<bool> Accept(SshHostKeyRequest request, CancellationToken ct) => ValueTask.FromResult(true);

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
        await using var tunnel = await new SshNetTunnelFactory(new InMemoryKnownHostsStore()).OpenAsync(Password(), SshJumpHost.Password, SshJumpFixture.DatabaseAlias, 5432, Accept, Ct);
        await AssertPostgresAnswersAsync(tunnel.LocalPort);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Key_without_passphrase_reaches_the_database()
    {
        await using var tunnel = await new SshNetTunnelFactory(new InMemoryKnownHostsStore()).OpenAsync(Key(fixture.Jump.PlainKeyPath), null, SshJumpFixture.DatabaseAlias, 5432, Accept, Ct);
        await AssertPostgresAnswersAsync(tunnel.LocalPort);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Key_with_passphrase_reaches_the_database()
    {
        await using var tunnel = await new SshNetTunnelFactory(new InMemoryKnownHostsStore()).OpenAsync(Key(fixture.Jump.EncryptedKeyPath), SshJumpHost.Passphrase, SshJumpFixture.DatabaseAlias, 5432, Accept, Ct);
        await AssertPostgresAnswersAsync(tunnel.LocalPort);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Wrong_password_fails_with_the_ssh_message()
    {
        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await new SshNetTunnelFactory(new InMemoryKnownHostsStore()).OpenAsync(Password(), "not-the-password", SshJumpFixture.DatabaseAlias, 5432, Accept, Ct));
        Assert.Contains("Permission denied", ex.Message, StringComparison.Ordinal);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Wrong_passphrase_fails()
    {
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await new SshNetTunnelFactory(new InMemoryKnownHostsStore()).OpenAsync(Key(fixture.Jump.EncryptedKeyPath), "wrong", SshJumpFixture.DatabaseAlias, 5432, Accept, Ct));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task DisposeAsync_frees_the_local_port()
    {
        var tunnel = await new SshNetTunnelFactory(new InMemoryKnownHostsStore()).OpenAsync(Password(), SshJumpHost.Password, SshJumpFixture.DatabaseAlias, 5432, Accept, Ct);
        var port = tunnel.LocalPort;
        await tunnel.DisposeAsync();

        using var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start(); // throws SocketException when the forward still owns the port
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task First_open_prompts_once_and_saves_then_second_open_does_not_prompt()
    {
        var store = new InMemoryKnownHostsStore();
        var factory = new SshNetTunnelFactory(store);
        var prompts = 0;
        ValueTask<bool> Counting(SshHostKeyRequest r, CancellationToken c)
        {
            prompts++;
            return ValueTask.FromResult(true);
        }

        await using (await factory.OpenAsync(Password(), SshJumpHost.Password, SshJumpFixture.DatabaseAlias, 5432, Counting, Ct)) { }
        await using (await factory.OpenAsync(Password(), SshJumpHost.Password, SshJumpFixture.DatabaseAlias, 5432, Counting, Ct)) { }

        Assert.Equal(1, prompts);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Unknown_key_without_a_prompt_is_rejected()
    {
        await Assert.ThrowsAsync<SshHostKeyRejectedException>(async () =>
            await new SshNetTunnelFactory(new InMemoryKnownHostsStore()).OpenAsync(Password(), SshJumpHost.Password, SshJumpFixture.DatabaseAlias, 5432, null, Ct));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Stored_wrong_fingerprint_throws_changed_and_never_prompts()
    {
        var store = new InMemoryKnownHostsStore();
        var factory = new SshNetTunnelFactory(store);
        var settings = Password();
        await using (await factory.OpenAsync(settings, SshJumpHost.Password, SshJumpFixture.DatabaseAlias, 5432, Accept, Ct)) { }

        SshHostKeyRequest? real = null;
        await using (await factory.OpenAsync(settings, SshJumpHost.Password, SshJumpFixture.DatabaseAlias, 5432, Accept, Ct)) { }
        foreach (var algorithm in new[] { "ssh-ed25519", "rsa-sha2-512", "rsa-sha2-256", "ecdsa-sha2-nistp256", "ssh-rsa" })
        {
            if (await store.FindAsync(settings.Host, settings.Port, algorithm, Ct) is not null)
            {
                real = new SshHostKeyRequest(settings.Host, settings.Port, algorithm, "SHA256:wrong");
                await store.SaveAsync(real, Ct);
            }
        }

        Assert.NotNull(real);
        var ex = await Assert.ThrowsAsync<SshHostKeyChangedException>(async () =>
            await factory.OpenAsync(settings, SshJumpHost.Password, SshJumpFixture.DatabaseAlias, 5432, (_, _) => throw new InvalidOperationException("prompted"), Ct));
        Assert.Equal("memory", ex.Location);
    }
}
