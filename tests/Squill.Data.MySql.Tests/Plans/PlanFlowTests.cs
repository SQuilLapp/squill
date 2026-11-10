using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.MySql.Tests;


public sealed class PlanFlowTests(MySqlFixture server) : IClassFixture<MySqlFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ExplainAndAnalyzeFlowsWorkOnMySql() =>
        await PlanFlowScenario.RunAsync(
            new MySqlDatabaseProvider(),
            new ConnectionProfile(Guid.NewGuid(), "my", DatabaseDialect.MySql, server.Host, server.Port, MySqlFixture.Database, MySqlFixture.UserName, null, null),
            MySqlFixture.Password,
            new PlanFlowSpec(true, "select count(*) from information_schema.columns a, information_schema.columns b, information_schema.columns c, information_schema.columns d", "@", null, null));
}
