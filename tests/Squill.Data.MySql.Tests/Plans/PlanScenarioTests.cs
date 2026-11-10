using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.MySql.Tests;

public sealed class PlanScenarioTests(MySqlFixture server) : IClassFixture<MySqlFixture>
{
    // Not sleep(): KILL QUERY makes sleep() return early without an error, so the statement would "succeed" instead of being cancelled.
    private const string Slow = "select count(*) from information_schema.columns a, information_schema.columns b, information_schema.columns c, information_schema.columns d";

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task SampleStatements_GiveUsablePlans_AndTheConnectionStillRuns()
    {
        await using var connection = await new MySqlDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "plan", DatabaseDialect.MySql, server.Host, server.Port, MySqlFixture.Database, MySqlFixture.UserName, null, null),
            MySqlFixture.Password,
            CancellationToken.None);
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", "mysql.sql"), TestContext.Current.CancellationToken);
        await using (var load = await connection.ExecuteAsync(script, [], CancellationToken.None))
            Assert.Null(await load.ReadBatchAsync(1, CancellationToken.None));
        var spec = new PlanSpec(DatabaseDialect.MySql, "", false, Slow, HasCost: true, HasActual: true, RolledBackOnActual: false, ActualFoldsConstantLookup: true);
        await PlanScenario.SeedAsync(connection, spec);
        await PlanScenario.RunAsync(connection, spec);
    }
}
