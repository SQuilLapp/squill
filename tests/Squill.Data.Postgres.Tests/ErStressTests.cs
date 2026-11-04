using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.Postgres.Tests;

/// <summary>Reads the 40-table er_stress group back from a real Postgres server and checks the ER diagram pipeline on it.</summary>
public sealed class ErStressTests(PostgresFixture server) : IClassFixture<PostgresFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ErStressGroup_ProducesAValidDiagram()
    {
        await using var connection = await new PostgresDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "er", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null),
            PostgresFixture.Password,
            CancellationToken.None);
        await ErStressScenario.RunAsync(connection, DatabaseDialect.PostgreSql);
    }
}
