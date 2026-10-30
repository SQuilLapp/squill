using Squill.Core;
using Squill.Data;
using Squill.Platform.Profiles;
using Squill.Platform.Ssh;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.Presentation.Tests.Sessions;

public sealed class SessionManagerTests
{
    private readonly List<string> _log = [];
    private readonly FakeProvider _provider;
    private readonly FakeTunnels _tunnels;
    private readonly FakeStore _store = new();
    private readonly SessionManager _manager;

    public SessionManagerTests()
    {
        _provider = new FakeProvider(_log);
        _tunnels = new FakeTunnels(_log);
        var registry = new ProviderRegistry();
        registry.Register(_provider);
        _manager = new SessionManager(registry, _store, _tunnels);
    }

    private static ConnectionProfile Profile(SshTunnelSettings? ssh = null) =>
        new(Guid.NewGuid(), "Prod", DatabaseDialect.PostgreSql, "db.internal", 5432, "app", "me", null, ssh);

    private static readonly SshTunnelSettings Ssh = new("jump", 22, "me", SshAuthMethod.Password, null);

    [Fact]
    public async Task OpenAsync_NoSsh_UsesProfileAndStoredPassword()
    {
        _store.Password = "pw";
        var profile = Profile();

        var session = await _manager.OpenAsync(profile, CancellationToken.None);

        Assert.Same(session, Assert.Single(_manager.Sessions));
        Assert.Equal(profile, _provider.OpenedWith);
        Assert.Equal("pw", _provider.Password);
        Assert.Equal(0, _tunnels.Opened);
    }

    [Fact]
    public async Task OpenAsync_Ssh_RewritesHostAndPort()
    {
        _store.SshSecret = "secret";

        var session = await _manager.OpenAsync(Profile(Ssh), CancellationToken.None);

        Assert.Equal(("db.internal", 5432, "secret"), (_tunnels.RemoteHost, _tunnels.RemotePort, _tunnels.Secret));
        Assert.Equal("127.0.0.1", _provider.OpenedWith!.Host);
        Assert.Equal(40123, _provider.OpenedWith.Port);
        Assert.Equal("db.internal", session.Profile.Host);
    }

    [Fact]
    public async Task CloseAsync_DisposesConnectionThenTunnel()
    {
        var session = await _manager.OpenAsync(Profile(Ssh), CancellationToken.None);
        _log.Clear();

        await _manager.CloseAsync(session);

        Assert.Equal(["connection disposed", "tunnel disposed"], _log);
        Assert.Empty(_manager.Sessions);
    }

    [Fact]
    public async Task OpenAsync_ConnectionFails_DisposesTunnel()
    {
        _provider.Failure = new InvalidOperationException("boom");

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await _manager.OpenAsync(Profile(Ssh), CancellationToken.None));

        Assert.Equal(["tunnel opened", "tunnel disposed"], _log);
        Assert.Empty(_manager.Sessions);
    }

    [Fact]
    public async Task OpenAsync_SchemaFails_DisposesConnectionThenTunnel()
    {
        _provider.SchemaFailure = new InvalidOperationException("schema");

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await _manager.OpenAsync(Profile(Ssh), CancellationToken.None));

        Assert.Equal(["tunnel opened", "connection disposed", "tunnel disposed"], _log);
    }

    [Fact]
    public async Task RefreshSchemaAsync_ReplacesSchema()
    {
        var session = await _manager.OpenAsync(Profile(), CancellationToken.None);
        var next = new SchemaSnapshot("public", []);
        _provider.Schema = next;

        await session.RefreshSchemaAsync(CancellationToken.None);

        Assert.Same(next, session.Schema);
    }

    private sealed class FakeStore : IProfileStore
    {
        public string? Password { get; set; }
        public string? SshSecret { get; set; }

        public ValueTask<IReadOnlyList<ConnectionProfile>> ListAsync(CancellationToken ct) => throw new NotSupportedException();
        public ValueTask SaveAsync(ConnectionProfile profile, string? password, string? sshSecret, CancellationToken ct) => throw new NotSupportedException();
        public ValueTask DeleteAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public ValueTask<string?> GetPasswordAsync(Guid id, CancellationToken ct) => ValueTask.FromResult(Password);
        public ValueTask<string?> GetSshSecretAsync(Guid id, CancellationToken ct) => ValueTask.FromResult(SshSecret);
    }

    private sealed class FakeTunnels(List<string> log) : ISshTunnelFactory
    {
        public int Opened { get; private set; }
        public string? RemoteHost { get; private set; }
        public int RemotePort { get; private set; }
        public string? Secret { get; private set; }

        public ValueTask<ISshTunnel> OpenAsync(SshTunnelSettings settings, string? secret, string remoteHost, int remotePort, CancellationToken ct)
        {
            Opened++;
            (RemoteHost, RemotePort, Secret) = (remoteHost, remotePort, secret);
            log.Add("tunnel opened");
            return ValueTask.FromResult<ISshTunnel>(new FakeTunnel(log));
        }
    }

    private sealed class FakeTunnel(List<string> log) : ISshTunnel
    {
        public int LocalPort => 40123;

        public ValueTask DisposeAsync()
        {
            log.Add("tunnel disposed");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeProvider(List<string> log) : IDatabaseProvider
    {
        public DatabaseDialect Dialect => DatabaseDialect.PostgreSql;
        public ConnectionProfile? OpenedWith { get; private set; }
        public string? Password { get; private set; }
        public Exception? Failure { get; set; }
        public Exception? SchemaFailure { get; set; }
        public SchemaSnapshot Schema { get; set; } = SchemaSnapshot.Empty;

        public ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile profile, string? password, CancellationToken cancellationToken)
        {
            if (Failure is { } failure)
                throw failure;
            (OpenedWith, Password) = (profile, password);
            return ValueTask.FromResult<IDatabaseConnection>(new FakeConnection(this, log));
        }
    }

    private sealed class FakeConnection(FakeProvider owner, List<string> log) : IDatabaseConnection
    {
        public string ServerVersion => "16.1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) =>
            owner.SchemaFailure is { } failure ? throw failure : ValueTask.FromResult(owner.Schema);

        public ValueTask DisposeAsync()
        {
            log.Add("connection disposed");
            return ValueTask.CompletedTask;
        }
    }
}
