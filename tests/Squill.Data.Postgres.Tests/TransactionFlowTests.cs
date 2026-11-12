using Squill.Core;
using Squill.Data.Tests.Scenarios;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class TransactionFlowTests(PostgresFixture server) : IClassFixture<PostgresFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task TransactionFlowsWorkOnPostgres() =>
        await TransactionFlowScenario.RunAsync(
            new PostgresDatabaseProvider(),
            new ConnectionProfile(Guid.NewGuid(), "pg", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null),
            PostgresFixture.Password,
            new TransactionFlowSpec("begin", "vacuum tf_t", true, false, true, true, PostgresFixture.Database));
}
