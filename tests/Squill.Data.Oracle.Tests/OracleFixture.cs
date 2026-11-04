using Testcontainers.Oracle;
using Xunit;

namespace Squill.Data.Oracle.Tests;

/// <summary>Starts one throw-away Oracle Free container for a test class and points the provider at the real bridge.</summary>
public sealed class OracleFixture : IAsyncLifetime
{
    public const string Service = "FREEPDB1";
    public const string UserName = "oracle";
    public const string Password = "squill-test";

    private OracleContainer? _container;
    private string? _previousBridge;

    public string Host => _container!.Hostname;

    public int Port => _container!.GetMappedPublicPort(1521);

    public async ValueTask InitializeAsync()
    {
        if (!DockerGate.Enabled)
            return;
        _previousBridge = Environment.GetEnvironmentVariable(BridgeClient.PathVariable);
        Environment.SetEnvironmentVariable(BridgeClient.PathVariable, Path.Combine(AppContext.BaseDirectory, "Squill.OracleBridge.dll"));
        _container = new OracleBuilder("gvenzl/oracle-free:23-slim-faststart")
            .WithUsername(UserName)
            .WithPassword(Password)
            .Build();
        await _container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        Environment.SetEnvironmentVariable(BridgeClient.PathVariable, _previousBridge);
        if (_container is not null)
            await _container.DisposeAsync();
    }
}
