using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using System.Security.Cryptography;
using Xunit;

namespace Squill.Tests.Containers;

/// <summary>
/// The <see cref="SshJumpFixture"/> network (PostgreSQL alias <c>db</c>, SSH alias <c>jump</c>, nothing of it published) plus a SOCKS5 proxy
/// (<c>serjs/go-socks5-proxy</c>, user and password per run) and an HTTP CONNECT proxy (<c>ubuntu/squid</c> with a minimal open configuration), both published on the host.
/// </summary>
public sealed class ProxyFixture : IAsyncLifetime
{
    public const string SocksUser = "squill";
    public static string SocksPassword { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(12));

    private readonly SshJumpFixture _network = new();
    private IContainer? _socks;
    private IContainer? _http;

    /// <summary>The database and SSH jump host behind the proxies.</summary>
    public SshJumpFixture Network => _network;

    public int SocksPort => _socks!.GetMappedPublicPort(1080);

    public int HttpPort => _http!.GetMappedPublicPort(3128);

    public async ValueTask InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("SQUILL_DOCKER_TESTS") != "1")
            return;
        await _network.InitializeAsync();
        _socks = new ContainerBuilder("serjs/go-socks5-proxy:latest")
            .WithNetwork(_network.Network)
            .WithPortBinding(1080, true)
            .WithEnvironment("PROXY_USER", SocksUser)
            .WithEnvironment("PROXY_PASSWORD", SocksPassword)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Start listening proxy service"))
            .Build();
        // Squid with no CONNECT port restriction: the default configuration only lets CONNECT reach 443.
        _http = new ContainerBuilder("ubuntu/squid:latest")
            .WithNetwork(_network.Network)
            .WithPortBinding(3128, true)
            .WithResourceMapping("http_port 3128\nacl all src all\nhttp_access allow all\n"u8.ToArray(), "/etc/squid/squid.conf")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Accepting HTTP Socket connections"))
            .Build();
        await Task.WhenAll(_socks.StartAsync(), _http.StartAsync());
    }

    public async ValueTask DisposeAsync()
    {
        if (_socks is not null)
            await _socks.DisposeAsync();
        if (_http is not null)
            await _http.DisposeAsync();
        await _network.DisposeAsync();
    }
}
