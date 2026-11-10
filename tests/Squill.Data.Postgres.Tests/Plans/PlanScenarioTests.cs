using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class PlanScenarioTests(PostgresFixture server) : IClassFixture<PostgresFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task SampleStatements_GiveUsablePlans_AndTheConnectionStillRuns()
    {
        await using var connection = await new PostgresDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "plan", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null),
            PostgresFixture.Password,
            CancellationToken.None);
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", "postgres.sql"), TestContext.Current.CancellationToken);
        await using (var load = await connection.ExecuteAsync(script, [], CancellationToken.None))
            Assert.Null(await load.ReadBatchAsync(1, CancellationToken.None));
        var spec = new PlanSpec(DatabaseDialect.PostgreSql, "sample.", false, "select pg_sleep(30)", HasCost: true, HasActual: true, RolledBackOnActual: true);
        await PlanScenario.SeedAsync(connection, spec);
        await PlanScenario.RunAsync(connection, spec);
    }
}
