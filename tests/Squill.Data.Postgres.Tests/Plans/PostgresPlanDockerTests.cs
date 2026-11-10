using Squill.Core;
using Squill.Core.Plans;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class PostgresPlanDockerTests(PostgresFixture server) : IClassFixture<PostgresFixture>
{
    private const string Join =
        "select c.name, sum(o.total) as t from sample.orders o join sample.customers c on c.id = o.customer_id where o.total > @min group by c.name order by t desc limit 5";

    private async Task<IDatabaseConnection> OpenAsync()
    {
        var connection = await new PostgresDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "plan", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null),
            PostgresFixture.Password,
            CancellationToken.None);
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", "postgres.sql"), TestContext.Current.CancellationToken);
        await using var reader = await connection.ExecuteAsync(script, [], CancellationToken.None);
        Assert.Null(await reader.ReadBatchAsync(1, CancellationToken.None));
        return connection;
    }

    private static async Task<List<object?[]>> RunAsync(IDatabaseConnection connection, string sql)
    {
        await using var reader = await connection.ExecuteAsync(sql, [], CancellationToken.None);
        return await reader.ReadAllAsync(100, CancellationToken.None);
    }

    private static async Task AssertStillRunsAsync(IDatabaseConnection connection) =>
        Assert.Equal(1, Assert.Single(await RunAsync(connection, "select 1"))[0]);

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Estimated_AndActual_OfTheSampleJoin_WithParameters()
    {
        await using var connection = await OpenAsync();
        var reader = (IPlanReader)connection;
        Assert.True(reader.SupportsActualPlan);
        QueryParameter[] parameters = [new("min", 0m)];

        var estimated = await reader.ExplainAsync(Join, parameters, PlanMode.Estimated, TestContext.Current.CancellationToken);
        Assert.NotNull(estimated.Root.EstimatedCost);
        Assert.Contains(estimated.Flatten(), n => n.Kind == PlanNodeKind.Join);
        Assert.Contains(estimated.Flatten(), n => n.Kind == PlanNodeKind.Sort);
        Assert.False(estimated.RolledBack);
        await AssertStillRunsAsync(connection);

        var actual = await reader.ExplainAsync(Join, parameters, PlanMode.Actual, TestContext.Current.CancellationToken);
        Assert.True(actual.RolledBack);
        Assert.All(actual.Flatten().Where(n => n.Kind == PlanNodeKind.TableScan), n => Assert.NotNull(n.ActualRows));
        Assert.NotNull(actual.ExecutionTimeMs);
        await AssertStillRunsAsync(connection);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Analyze_OfAWritingFunction_FailsAndLeavesNoRow()
    {
        await using var connection = await OpenAsync();
        await RunAsync(connection, "create or replace function sample.plan_writer() returns integer language plpgsql as $$ begin insert into sample.fk_targets (id) values (987654); return 1; end $$");
        var before = await RunAsync(connection, "select count(*) from sample.fk_targets where id = 987654");
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await ((IPlanReader)connection).ExplainAsync("select sample.plan_writer()", [], PlanMode.Actual, TestContext.Current.CancellationToken));
        Assert.Equal(before[0][0], (await RunAsync(connection, "select count(*) from sample.fk_targets where id = 987654"))[0][0]);
        Assert.Equal(0L, before[0][0]);
        await AssertStillRunsAsync(connection);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Analyze_InsideAUserTransaction_KeepsTheTransaction()
    {
        await using var connection = await OpenAsync();
        await RunAsync(connection, "begin");
        await RunAsync(connection, "insert into sample.fk_targets (id) values (987655)");
        var plan = await ((IPlanReader)connection).ExplainAsync("select count(*) from sample.fk_targets", [], PlanMode.Actual, TestContext.Current.CancellationToken);
        Assert.False(plan.RolledBack);
        Assert.Equal(1L, (await RunAsync(connection, "select count(*) from sample.fk_targets where id = 987655"))[0][0]);
        await RunAsync(connection, "rollback");
        Assert.Equal(0L, (await RunAsync(connection, "select count(*) from sample.fk_targets where id = 987655"))[0][0]);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task CancelDuringAnalyze_ThrowsAndTheConnectionStillRuns()
    {
        await using var connection = await OpenAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await ((IPlanReader)connection).ExplainAsync("select pg_sleep(30)", [], PlanMode.Actual, cts.Token));
        await AssertStillRunsAsync(connection);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task SyntaxError_PropagatesAndTheConnectionStillRuns()
    {
        await using var connection = await OpenAsync();
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await ((IPlanReader)connection).ExplainAsync("select from from", [], PlanMode.Actual, TestContext.Current.CancellationToken));
        await AssertStillRunsAsync(connection);
    }
}
