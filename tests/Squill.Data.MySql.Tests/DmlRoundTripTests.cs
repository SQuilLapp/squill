using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.MySql.Tests;

public sealed class DmlRoundTripTests(MySqlFixture server) : IClassFixture<MySqlFixture>
{
    private async Task<IDatabaseConnection> OpenAsync() => await new MySqlDatabaseProvider().OpenAsync(
        new ConnectionProfile(Guid.NewGuid(), "dml", DatabaseDialect.MySql, server.Host, server.Port, MySqlFixture.Database, MySqlFixture.UserName, null, null),
        MySqlFixture.Password,
        CancellationToken.None);

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task GeneratedStatements_ApplyAndReadBack()
    {
        await using var connection = await OpenAsync();
        await DmlScenario.RunAsync(connection, DatabaseDialect.MySql, null);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task TypedParameters_ReadBackEqual()
    {
        await using var connection = await OpenAsync();
        await DmlScenario.RunTypedAsync(connection, DatabaseDialect.MySql, null);
    }
}
