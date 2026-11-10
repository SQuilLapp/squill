using Squill.Core;
using Squill.Core.Plans;
using Squill.Sql.Plans;
using Xunit;

namespace Squill.Data.MySql.Tests;

public sealed class MySqlPlanDockerTests(MySqlFixture server) : IClassFixture<MySqlFixture>
{
    private const string Join =
        "select c.name, sum(o.total) as t from orders o join customers c on c.id = o.customer_id where o.total > @min group by c.name order by t desc limit 5";

    // Not sleep(): KILL QUERY makes sleep() return early without an error, so the statement would "succeed" instead of being cancelled.
    private const string Slow = "select count(*) from information_schema.columns a, information_schema.columns b, information_schema.columns c, information_schema.columns d";

    private async Task<IDatabaseConnection> OpenAsync()
    {
        var connection = await new MySqlDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "plan", DatabaseDialect.MySql, server.Host, server.Port, MySqlFixture.Database, MySqlFixture.UserName, null, null),
            MySqlFixture.Password,
            CancellationToken.None);
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", "mysql.sql"), TestContext.Current.CancellationToken);
        await using var reader = await connection.ExecuteAsync(script, [], CancellationToken.None);
        Assert.Null(await reader.ReadBatchAsync(1, CancellationToken.None));
        return connection;
    }

    private static async Task AssertStillRunsAsync(IDatabaseConnection connection)
    {
        await using var reader = await connection.ExecuteAsync("select 1", [], CancellationToken.None);
        var rows = await reader.ReadAllAsync(10, CancellationToken.None);
        Assert.Equal(1L, Convert.ToInt64(Assert.Single(rows)[0], System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Estimated_PlanOfTheSampleJoin_WithAParameter()
    {
        await using var connection = await OpenAsync();
        var reader = (IPlanReader)connection;
        Assert.True(reader.SupportsActualPlan);
        var plan = await reader.ExplainAsync(Join, [new QueryParameter("min", 0m)], PlanMode.Estimated, TestContext.Current.CancellationToken);
        Assert.Equal(PlanMode.Estimated, plan.Mode);
        Assert.NotNull(plan.Root.EstimatedCost);
        Assert.Contains(plan.Flatten(), n => n.Kind == PlanNodeKind.Join);
        Assert.Contains(plan.Flatten(), n => n.Kind == PlanNodeKind.Sort);
        var targets = plan.Flatten().Select(n => n.Target).OfType<string>().ToList();
        Assert.Contains(targets, t => t.StartsWith('o'));
        Assert.Contains(targets, t => t.StartsWith('c'));
        Assert.All(plan.Flatten().Where(n => n.Kind is PlanNodeKind.TableScan or PlanNodeKind.IndexScan or PlanNodeKind.IndexSeek), n => Assert.NotNull(n.EstimatedRows));
        await AssertStillRunsAsync(connection);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Estimated_DoesNotExecuteTheStatement_AndErrorsPropagate()
    {
        await using var connection = await OpenAsync();
        var reader = (IPlanReader)connection;
        var plan = await reader.ExplainAsync("select sleep(30)", [], PlanMode.Estimated, TestContext.Current.CancellationToken);
        Assert.NotNull(plan.Root);
        await Assert.ThrowsAnyAsync<Exception>(async () => await reader.ExplainAsync("select from from", [], PlanMode.Estimated, TestContext.Current.CancellationToken));
        await AssertStillRunsAsync(connection);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Actual_PlanOfTheSampleJoin_HasActualRowsOnTheScans()
    {
        await using var connection = await OpenAsync();
        await using (var seed = await connection.ExecuteAsync(
            "insert into customers (name) values ('a'), ('b'); insert into orders (customer_id, total) select id, 5 from customers", [], CancellationToken.None))
            await seed.ReadAllAsync(10, CancellationToken.None);
        var plan = await ((IPlanReader)connection).ExplainAsync(Join, [new QueryParameter("min", 0m)], PlanMode.Actual, TestContext.Current.CancellationToken);
        Assert.Equal(PlanMode.Actual, plan.Mode);
        Assert.False(plan.RolledBack);
        Assert.NotNull(plan.ExecutionTimeMs);
        var scans = plan.Flatten().Where(n => n.Kind is PlanNodeKind.TableScan or PlanNodeKind.IndexScan or PlanNodeKind.IndexSeek && n.Target is not null).ToList();
        Assert.NotEmpty(scans);
        Assert.Contains(scans, n => n.ActualRows > 0);
        Assert.All(scans, n => Assert.NotNull(n.ActualRows));
        Assert.Contains(plan.Flatten(), n => n.Kind == PlanNodeKind.Sort);
        await AssertStillRunsAsync(connection);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Actual_OfAnInsert_IsRefusedByTheGuard_AndNothingIsWritten()
    {
        await using var connection = await OpenAsync();
        var eligibility = PlanEligibility.Evaluate("insert into customers (name) values ('x')", DatabaseDialect.MySql);
        Assert.True(eligibility.CanExplain);
        Assert.False(eligibility.CanAnalyze);
        await using var reader = await connection.ExecuteAsync("select count(*) from customers where name = 'x'", [], CancellationToken.None);
        Assert.Equal(0L, Convert.ToInt64(Assert.Single(await reader.ReadAllAsync(10, CancellationToken.None))[0], System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task CancelDuringAnalyze_ThrowsAndTheConnectionStillRuns()
    {
        await using var connection = await OpenAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await ((IPlanReader)connection).ExplainAsync(Slow, [], PlanMode.Actual, cts.Token));
        await AssertStillRunsAsync(connection);
    }
}
