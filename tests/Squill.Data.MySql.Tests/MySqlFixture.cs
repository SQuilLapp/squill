using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Testcontainers.MySql;
using Xunit;

namespace Squill.Data.MySql.Tests;

/// <summary>Starts one throw-away MySQL container for a test class; <c>SQUILL_MYSQL_IMAGE</c> swaps the image (the MariaDB run).</summary>
public sealed class MySqlFixture : IAsyncLifetime
{
    public const string Database = "squill";
    public const string UserName = "root";
    public const string Password = "squill-test";

    /// <summary>Whether the run targets MariaDB (<c>SQUILL_MYSQL_IMAGE</c> is set).</summary>
    public static bool IsMariaDb => Environment.GetEnvironmentVariable("SQUILL_MYSQL_IMAGE") is { Length: > 0 };

    private IContainer? _container;

    public string Host => _container!.Hostname;

    public int Port => _container!.GetMappedPublicPort(3306);

    public async ValueTask InitializeAsync()
    {
        if (!DockerGate.Enabled)
            return;
        // The MySQL module waits with the `mysql` client, which mariadb:11 no longer ships.
        _container = IsMariaDb
            ? new ContainerBuilder(Environment.GetEnvironmentVariable("SQUILL_MYSQL_IMAGE")!).WithPortBinding(3306, true)
                .WithEnvironment("MARIADB_ROOT_PASSWORD", Password).WithEnvironment("MARIADB_DATABASE", Database)
                .WithCommand("--max-recursive-iterations=100000000") // MariaDB has neither cte_max_recursion_depth nor the SET_VAR hint
                .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("mariadb-admin", "ping", "-h127.0.0.1", "-uroot", "-p" + Password, "--silent"))
                .Build()
            : new MySqlBuilder("mysql:8.4").WithDatabase(Database).WithPassword(Password).Build();
        await _container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}
