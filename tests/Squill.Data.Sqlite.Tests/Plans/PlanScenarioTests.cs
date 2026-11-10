using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.Sqlite.Tests;

public sealed class PlanScenarioTests
{
    [Fact]
    public async Task SampleStatements_GiveUsablePlans_AndTheConnectionStillRuns()
    {
        var connection = await new SqliteDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "plan", DatabaseDialect.Sqlite, null, null, null, null, ":memory:", null), null, TestContext.Current.CancellationToken);
        await using (connection)
        {
            var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", "sqlite.sql"), TestContext.Current.CancellationToken);
            await using (var load = await connection.ExecuteAsync(script, [], CancellationToken.None))
                Assert.Null(await load.ReadBatchAsync(1, CancellationToken.None));
            var spec = new PlanSpec(DatabaseDialect.Sqlite, "", false, "", HasCost: false, HasActual: false, RolledBackOnActual: false, HasJoinNode: false);
            await PlanScenario.SeedAsync(connection, spec);
            await PlanScenario.RunAsync(connection, spec);
        }
    }
}
