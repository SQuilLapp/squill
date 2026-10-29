using Testcontainers.MySql;
using Xunit;

namespace Squill.Data.MySql.Tests;

/// <summary>Starts one throw-away MySQL container for a test class.</summary>
public sealed class MySqlFixture : IAsyncLifetime
{
    public const string Database = "squill";
    public const string UserName = "root";
    public const string Password = "squill-test";

    private MySqlContainer? _container;

    public string Host => _container!.Hostname;

    public int Port => _container!.GetMappedPublicPort(3306);

    public async ValueTask InitializeAsync()
    {
        if (!DockerGate.Enabled)
            return;
        _container = new MySqlBuilder("mysql:8.4").WithDatabase(Database).WithPassword(Password).Build();
        await _container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}
