using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.MySql.Tests;

public sealed class GridFilterFlowTests(MySqlFixture server) : IClassFixture<MySqlFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task GridFilterFlowsWorkOnMySql() =>
        await GridFilterFlowScenario.RunAsync(
            new MySqlDatabaseProvider(),
            new ConnectionProfile(Guid.NewGuid(), "my", DatabaseDialect.MySql, server.Host, server.Port, MySqlFixture.Database, MySqlFixture.UserName, null, null),
            MySqlFixture.Password,
            new GridFilterSpec("decimal(10,2)", "varchar(100)", "", "datetime", "boolean", "true", "false", "char(36)", "with recursive s(n) as (select 1 union all select n + 1 from s where n < 50000) select /*+ SET_VAR(cte_max_recursion_depth = 200000) */ n from s"));
}
