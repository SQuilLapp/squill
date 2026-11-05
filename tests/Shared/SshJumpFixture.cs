using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using System.Security.Cryptography;
using Xunit;

namespace Squill.Tests.Containers;

/// <summary>A linuxserver/openssh-server container published on the host.</summary>
/// <remarks>
/// Environment variables the image documents: <c>PUID</c>/<c>PGID</c>, <c>USER_NAME</c>, <c>USER_PASSWORD</c> and <c>PASSWORD_ACCESS=true</c> (password login), <c>LOG_STDOUT=true</c>.
/// The image ships <c>AllowTcpForwarding no</c> and has no variable for it: a <c>sshd_config.d/forward.conf</c> is copied in before start, which the image includes when that folder exists (no Docker mod).
/// Key logins use ed25519 keys made by <c>ssh-keygen</c> inside the container: one without and one with a passphrase, both appended to <c>authorized_keys</c>.
/// </remarks>
public sealed class SshJumpHost : IAsyncDisposable
{
    public const string UserName = "squill";
    /// <summary>Random per run, so no credential sits in the source.</summary>
    public static string Password { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(12));

    /// <summary>Random per run passphrase of the encrypted key.</summary>
    public static string Passphrase { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(12));

    private readonly IContainer _container;
    private readonly string _keyDirectory;

    internal SshJumpHost(IContainer container, string keyDirectory, string plainKeyPath, string encryptedKeyPath)
    {
        (_container, _keyDirectory, PlainKeyPath, EncryptedKeyPath) = (container, keyDirectory, plainKeyPath, encryptedKeyPath);
    }

    /// <summary>Host name to reach the container from the test process.</summary>
    public string Host => _container.Hostname;

    /// <summary>Published port of the SSH server; it can change after a restart.</summary>
    public int Port => _container.GetMappedPublicPort(2222);

    /// <summary>Private key file without a passphrase.</summary>
    public string PlainKeyPath { get; }

    /// <summary>Private key file protected by <see cref="Passphrase"/>.</summary>
    public string EncryptedKeyPath { get; }

    public static async Task<SshJumpHost> StartAsync(INetwork network, string alias, CancellationToken ct)
    {
        var container = new ContainerBuilder("lscr.io/linuxserver/openssh-server:latest")
            .WithNetwork(network)
            .WithNetworkAliases(alias)
            .WithPortBinding(2222, true)
            .WithEnvironment("PUID", "1000")
            .WithEnvironment("PGID", "1000")
            .WithEnvironment("USER_NAME", UserName)
            .WithEnvironment("USER_PASSWORD", Password)
            .WithEnvironment("PASSWORD_ACCESS", "true")
            .WithEnvironment("LOG_STDOUT", "true")
            .WithResourceMapping("AllowTcpForwarding yes\nGatewayPorts no\n"u8.ToArray(), "/config/sshd/sshd_config.d/forward.conf")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Server listening on"))
            .Build();
        await container.StartAsync(ct);

        var directory = Directory.CreateTempSubdirectory("squill-ssh-").FullName;
        var made = await container.ExecAsync(["sh", "-c",
            $"mkdir -p /config/keys && ssh-keygen -q -t ed25519 -N '' -f /config/keys/plain && ssh-keygen -q -t ed25519 -N '{Passphrase}' -f /config/keys/enc && cat /config/keys/plain.pub /config/keys/enc.pub >> /config/.ssh/authorized_keys"], ct);
        if (made.ExitCode != 0)
            throw new InvalidOperationException("ssh-keygen failed in the SSH container: " + made.Stderr);
        var plain = Path.Combine(directory, "plain");
        var encrypted = Path.Combine(directory, "encrypted");
        await File.WriteAllBytesAsync(plain, await container.ReadFileAsync("/config/keys/plain", ct), ct);
        await File.WriteAllBytesAsync(encrypted, await container.ReadFileAsync("/config/keys/enc", ct), ct);
        return new SshJumpHost(container, directory, plain, encrypted);
    }

    public Task StopAsync(CancellationToken ct) => _container.StopAsync(ct);

    public Task StartAgainAsync(CancellationToken ct) => _container.StartAsync(ct);

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
        try { Directory.Delete(_keyDirectory, true); } catch (IOException) { }
    }
}

/// <summary>A network with a PostgreSQL container that has no published port (alias <see cref="DatabaseAlias"/>) and an SSH jump host in front of it.</summary>
public sealed class SshJumpFixture : IAsyncLifetime
{
    public const string DatabaseAlias = "db";
    public static string DatabasePassword { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(12));

    private INetwork? _network;
    private IContainer? _database;

    /// <summary>The jump host shared by the tests of a class.</summary>
    public SshJumpHost Jump { get; private set; } = null!;

    /// <summary>The network, to start more containers (for example a second jump host).</summary>
    public INetwork Network => _network!;

    public async ValueTask InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("SQUILL_DOCKER_TESTS") != "1")
            return;
        _network = new NetworkBuilder().Build();
        await _network.CreateAsync();
        // Nothing is published: the database is reachable only through the jump host. pg_isready on 127.0.0.1 ignores the init-time server, which listens on the socket only.
        _database = new ContainerBuilder("postgres:17-alpine")
            .WithNetwork(_network)
            .WithNetworkAliases(DatabaseAlias)
            .WithEnvironment("POSTGRES_PASSWORD", DatabasePassword)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("pg_isready", "-h", "127.0.0.1", "-U", "postgres"))
            .Build();
        await _database.StartAsync();
        Jump = await SshJumpHost.StartAsync(_network, "jump", CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        if (Jump is not null)
            await Jump.DisposeAsync();
        if (_database is not null)
            await _database.DisposeAsync();
        if (_network is not null)
            await _network.DeleteAsync();
    }
}
