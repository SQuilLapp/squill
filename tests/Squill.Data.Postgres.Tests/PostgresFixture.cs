using Testcontainers.PostgreSql;
using Xunit;

namespace Squill.Data.Postgres.Tests;

/// <summary>Starts one throw-away PostgreSQL container for a test class.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string Database = "postgres";
    public const string UserName = "postgres";
    public const string Password = "squill-test";

    private PostgreSqlContainer? _container;

    public string Host => _container!.Hostname;

    public int Port => _container!.GetMappedPublicPort(5432);

    public async ValueTask InitializeAsync()
    {
        if (!DockerGate.Enabled)
            return;
        _container = new PostgreSqlBuilder("postgres:17-alpine").WithPassword(Password).Build();
        await _container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}
