using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.SqlServer.Tests;


public sealed class PlanFlowTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ExplainAndAnalyzeFlowsWorkOnSqlServer() =>
        await PlanFlowScenario.RunAsync(
            new SqlServerDatabaseProvider(),
            new ConnectionProfile(Guid.NewGuid(), "ms", DatabaseDialect.SqlServer, "127.0.0.1", server.Port, SqlServerFixture.Database, SqlServerFixture.UserName, null, null),
            SqlServerFixture.Password,
            new PlanFlowSpec(true, "select count_big(*) from sys.all_objects a cross join sys.all_objects b cross join sys.all_objects c cross join sys.all_objects d", "@", null, null));
}
