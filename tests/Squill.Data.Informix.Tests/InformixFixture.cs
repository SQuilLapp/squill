using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace Squill.Data.Informix.Tests;

/// <summary>Gate for tests that need Docker: they run only when <c>SQUILL_DOCKER_TESTS=1</c>.</summary>
public static class DockerGate
{
    public const string SkipReason = "Set SQUILL_DOCKER_TESTS=1 to run the Docker tests.";

    public static bool Enabled => Environment.GetEnvironmentVariable("SQUILL_DOCKER_TESTS") == "1";
}

/// <summary>
/// Starts the IBM Informix developer container and points the layer at IBM's <c>clidriver</c>. The test never downloads it: it uses
/// <c>SQUILL_CLIDRIVER</c> when set, else the <c>clidriver</c> folder of the <c>Net.IBM.Data.Db2</c> package in the NuGet cache
/// (<c>NUGET_PACKAGES</c> or <c>~/.nuget/packages</c>, restored by <c>dotnet restore</c> of any project that references the package).
/// </summary>
public sealed class InformixFixture : IAsyncLifetime
{
    public const string UserName = "informix";
    public const string Password = "in4mix";
    public const string Database = "sysmaster";

    private IContainer? _container;

    public static string? FindClidriver()
    {
        var configured = Environment.GetEnvironmentVariable(ClidriverLocator.Variable);
        if (!string.IsNullOrWhiteSpace(configured))
            return File.Exists(Path.Combine(configured, "bin", "db2cli64.dll")) ? configured : null;
        var packages = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        var package = Path.Combine(packages, "net.ibm.data.db2");
        return Directory.Exists(package)
            ? Directory.EnumerateDirectories(package)
                .Select(version => Path.Combine(version, "buildTransitive", "clidriver"))
                .FirstOrDefault(folder => File.Exists(Path.Combine(folder, "bin", "db2cli64.dll")))
            : null;
    }

    public static string Host => "127.0.0.1";

    public int Port => _container!.GetMappedPublicPort(9089);

    /// <summary>Gets a database created with a transaction log, for the transaction tests.</summary>
    public const string LoggedDatabase = "sqlog";

    /// <summary>Gets a database created without a log, where transactions are not supported.</summary>
    public const string UnloggedDatabase = "sqnolog";

    public string ConnectionString(string password = Password, string database = Database) => OdbcConnectionString.Build(Host, Port, database, UserName, password);

    public async ValueTask InitializeAsync()
    {
        var clidriver = FindClidriver();
        if (!DockerGate.Enabled || clidriver is null)
            return;
        Environment.SetEnvironmentVariable(ClidriverLocator.Variable, clidriver);
        _container = new ContainerBuilder("icr.io/informix/informix-developer-database:latest")
            .WithEnvironment("LICENSE", "accept")
            .WithPortBinding(9089, true)
            .Build();
        await _container.StartAsync();

        // The DRDA port answers before the engine is online: retry a real connect (this also exercises the layer).
        var deadline = DateTime.UtcNow.AddMinutes(4);
        while (true)
        {
            try
            {
                using var connection = await OdbcConnection.OpenAsync(ConnectionString(), TestContext.Current.CancellationToken);
                (await connection.ExecuteAsync("create database " + LoggedDatabase + " with log", TestContext.Current.CancellationToken)).Dispose();
                (await connection.ExecuteAsync("create database " + UnloggedDatabase, TestContext.Current.CancellationToken)).Dispose();
                return;
            }
            catch (OdbcException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }
}
