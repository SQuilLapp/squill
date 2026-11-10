using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class JoinCompletionScenarioTests(PostgresFixture server) : IClassFixture<PostgresFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task GeneratedJoinConditions_RunOnTheEngine()
    {
        await using var connection = await new PostgresDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "join-completion", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null),
            PostgresFixture.Password,
            CancellationToken.None);
        await JoinCompletionScenario.RunAsync(connection, new JoinCompletionSpec(DatabaseDialect.PostgreSql, "sample.", "integer", "postgres.sql"));
    }
}
