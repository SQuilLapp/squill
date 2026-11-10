using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.Postgres.Tests;


public sealed class PlanFlowTests(PostgresFixture server) : IClassFixture<PostgresFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ExplainAndAnalyzeFlowsWorkOnPostgres() =>
        await PlanFlowScenario.RunAsync(
            new PostgresDatabaseProvider(),
            new ConnectionProfile(Guid.NewGuid(), "pg", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null),
            PostgresFixture.Password,
            new PlanFlowSpec(true, "select pg_sleep(30)", "@", "create table pf_log (n integer); create function pf_write() returns integer language sql volatile as 'insert into pf_log values (1); select 1'", "select pf_write()"));
}
