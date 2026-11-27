using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.Oracle.Tests;

public sealed class DmlRoundTripTests(OracleFixture server) : IClassFixture<OracleFixture>
{
    private ValueTask<IDatabaseConnection> OpenAsync() =>
        new OracleDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "ora", DatabaseDialect.Oracle, server.Host, server.Port, OracleFixture.Service, OracleFixture.UserName, null, null),
            OracleFixture.Password, CancellationToken.None);

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task KeysDefaultsUpdatesAndDeletes_RoundTrip()
    {
        await using var connection = await OpenAsync();
        Assert.Null(await Record.ExceptionAsync(() => DmlScenario.RunAsync(connection, DatabaseDialect.Oracle, null)));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task EveryEditableClrType_RoundTrips()
    {
        await using var connection = await OpenAsync();
        Assert.Null(await Record.ExceptionAsync(() => DmlScenario.RunTypedAsync(connection, DatabaseDialect.Oracle, null)));
    }
}
