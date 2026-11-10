using Squill.Core;
using Squill.Data.RoundTrip;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

public sealed class PlanScenarioTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task SampleStatements_GiveUsablePlans_AndTheConnectionStillRuns()
    {
        await using var connection = await new SqlServerDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "plan", DatabaseDialect.SqlServer, server.Host, server.Port, SqlServerFixture.Database, SqlServerFixture.UserName, null, null),
            SqlServerFixture.Password,
            CancellationToken.None);

        // GO is a client convention, not T-SQL: split the script on the lines that hold only GO.
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", "sqlserver.sql"), TestContext.Current.CancellationToken);
        foreach (var batch in script.Split("\nGO", StringSplitOptions.None))
        {
            await using var load = await connection.ExecuteAsync(batch, [], CancellationToken.None);
            Assert.Null(await load.ReadBatchAsync(1, CancellationToken.None));
        }

        var spec = new PlanSpec(DatabaseDialect.SqlServer, "sample.", true, "waitfor delay '00:00:30'", HasCost: true, HasActual: true, RolledBackOnActual: true);
        await PlanScenario.SeedAsync(connection, spec);
        await PlanScenario.RunAsync(connection, spec);
    }
}
