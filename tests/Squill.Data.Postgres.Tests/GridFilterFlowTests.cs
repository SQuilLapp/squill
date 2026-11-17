using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class GridFilterFlowTests(PostgresFixture server) : IClassFixture<PostgresFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task GridFilterFlowsWorkOnPostgres() =>
        await GridFilterFlowScenario.RunAsync(
            new PostgresDatabaseProvider(),
            new ConnectionProfile(Guid.NewGuid(), "pg", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null),
            PostgresFixture.Password,
            new GridFilterSpec("numeric(10,2)", "varchar(100)", "", "timestamp", "boolean", "true", "false", "uuid", "select g as n from generate_series(1, 50000) g"));
}
