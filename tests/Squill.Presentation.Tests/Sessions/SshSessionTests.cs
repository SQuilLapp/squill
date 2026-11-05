using System.Net;
using System.Net.Sockets;
using Squill.Core;
using Squill.Data;
using Squill.Data.Postgres;
using Squill.Platform.Profiles;
using Squill.Platform.Ssh;
using Squill.Presentation.Sessions;
using Squill.Tests.Containers;
using Xunit;

namespace Squill.Presentation.Tests.Sessions;

/// <summary><see cref="SessionManager"/> with the real SSH tunnel and PostgreSQL provider, the database being reachable only through a jump host.</summary>
public sealed class SshSessionTests(SshJumpFixture fixture) : IClassFixture<SshJumpFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SessionManager NewManager(string? sshSecret)
    {
        var registry = new ProviderRegistry();
        registry.Register(new PostgresDatabaseProvider());
        return new SessionManager(registry, new Store(sshSecret), new SshNetTunnelFactory());
    }

    private static ConnectionProfile Profile(SshJumpHost jump, SshAuthMethod method, string? keyPath) =>
        new(Guid.NewGuid(), "ssh", DatabaseDialect.PostgreSql, SshJumpFixture.DatabaseAlias, 5432, "postgres", "postgres", null,
            new SshTunnelSettings(jump.Host, jump.Port, SshJumpHost.UserName, method, keyPath));

    private static async Task AssertSelectOneAsync(ConnectionSession session)
    {
        await using var reader = await session.Connection.ExecuteAsync("select 1", [], Ct);
        var rows = await reader.ReadAllAsync(10, Ct);
        Assert.Equal(1, Convert.ToInt32(Assert.Single(rows)[0], System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Password_session_queries_and_closes_the_tunnel()
    {
        var manager = NewManager(SshJumpHost.Password);
        var session = await manager.OpenAsync(Profile(fixture.Jump, SshAuthMethod.Password, null), Ct);
        await AssertSelectOneAsync(session);
        var port = session.TunnelPort!.Value;

        await manager.CloseAsync(session);

        Assert.Empty(manager.Sessions);
        using var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start(); // the local forward is gone
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Key_without_passphrase_session_queries()
    {
        var manager = NewManager(null);
        await using var session = await manager.OpenAsync(Profile(fixture.Jump, SshAuthMethod.PrivateKey, fixture.Jump.PlainKeyPath), Ct);
        await AssertSelectOneAsync(session);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Key_with_passphrase_session_queries()
    {
        var manager = NewManager(SshJumpHost.Passphrase);
        await using var session = await manager.OpenAsync(Profile(fixture.Jump, SshAuthMethod.PrivateKey, fixture.Jump.EncryptedKeyPath), Ct);
        await AssertSelectOneAsync(session);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Wrong_password_fails_and_leaves_nothing_open()
    {
        var manager = NewManager("not-the-password");

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await manager.OpenAsync(Profile(fixture.Jump, SshAuthMethod.Password, null), Ct));

        Assert.Contains("Permission denied", ex.Message, StringComparison.Ordinal);
        Assert.Empty(manager.Sessions);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Dropping_the_jump_host_fails_the_next_query_and_the_session_reopens()
    {
        await using var jump = await SshJumpHost.StartAsync(fixture.Network, "jump2", Ct);
        var manager = NewManager(SshJumpHost.Password);
        var profile = Profile(jump, SshAuthMethod.Password, null);
        var session = await manager.OpenAsync(profile, Ct);
        await AssertSelectOneAsync(session);

        await jump.StopAsync(Ct);

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () => await AssertSelectOneAsync(session));
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
        await manager.CloseAsync(session);

        await jump.StartAgainAsync(Ct);
        await using var reopened = await manager.OpenAsync(Profile(jump, SshAuthMethod.Password, null), Ct);
        await AssertSelectOneAsync(reopened);
    }

    private sealed class Store(string? sshSecret) : IProfileStore
    {
        public ValueTask<IReadOnlyList<ConnectionProfile>> ListAsync(CancellationToken ct) => throw new NotSupportedException();
        public ValueTask SaveAsync(ConnectionProfile profile, string? password, string? sshSecret, CancellationToken ct) => throw new NotSupportedException();
        public ValueTask DeleteAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public ValueTask<string?> GetPasswordAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>(SshJumpFixture.DatabasePassword);
        public ValueTask<string?> GetSshSecretAsync(Guid id, CancellationToken ct) => ValueTask.FromResult(sshSecret);
    }
}
