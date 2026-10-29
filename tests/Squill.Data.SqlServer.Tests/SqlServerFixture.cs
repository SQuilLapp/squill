using Testcontainers.MsSql;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

/// <summary>Starts one throw-away SQL Server container for a test class.</summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string Database = "master";
    public const string UserName = "sa";
    public const string Password = "Squill-Test-1234";

    private MsSqlContainer? _container;

    public string Host => _container!.Hostname;

    public int Port => _container!.GetMappedPublicPort(1433);

    public async ValueTask InitializeAsync()
    {
        if (!DockerGate.Enabled)
            return;
        _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").WithPassword(Password).Build();
        await _container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}
