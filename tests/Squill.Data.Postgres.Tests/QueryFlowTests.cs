using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class QueryFlowTests(PostgresFixture server) : IClassFixture<PostgresFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task QueryTabFlowsWorkOnPostgres() =>
        await QueryFlowScenario.RunAsync(
            new PostgresDatabaseProvider(),
            new ConnectionProfile(Guid.NewGuid(), "pg", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null),
            PostgresFixture.Password,
            new QueryFlowSpec("select g from generate_series(1, 100000) g", "select pg_sleep(30)", "@", "$1", "select * from public.qf_", "qf_t"));
}
