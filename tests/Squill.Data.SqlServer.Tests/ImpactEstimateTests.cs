using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

public sealed class ImpactEstimateTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task PlannerEstimateIsWithinAFactorOfTwoAndNothingIsWritten()
    {
        await ImpactEstimateScenario.RunAsync(new SqlServerDatabaseProvider(), new ConnectionProfile(Guid.NewGuid(), "ms", DatabaseDialect.SqlServer, server.Host, server.Port, SqlServerFixture.Database, SqlServerFixture.UserName, null, null), SqlServerFixture.Password);
    }
}
