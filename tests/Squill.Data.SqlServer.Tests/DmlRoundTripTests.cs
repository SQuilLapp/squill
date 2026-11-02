using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

public sealed class DmlRoundTripTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    private async Task<IDatabaseConnection> OpenAsync() => await new SqlServerDatabaseProvider().OpenAsync(
        new ConnectionProfile(Guid.NewGuid(), "dml", DatabaseDialect.SqlServer, server.Host, server.Port, SqlServerFixture.Database, SqlServerFixture.UserName, null, null),
        SqlServerFixture.Password,
        CancellationToken.None);

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task GeneratedStatements_ApplyAndReadBack()
    {
        await using var connection = await OpenAsync();
        await DmlScenario.RunAsync(connection, DatabaseDialect.SqlServer, "dbo");
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task TypedParameters_ReadBackEqual()
    {
        await using var connection = await OpenAsync();
        await DmlScenario.RunTypedAsync(connection, DatabaseDialect.SqlServer, "dbo");
    }
}
