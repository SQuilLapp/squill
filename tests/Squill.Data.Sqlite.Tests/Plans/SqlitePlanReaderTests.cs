using Squill.Core;
using Squill.Core.Plans;
using Xunit;

namespace Squill.Data.Sqlite.Tests;

public sealed class SqlitePlanReaderTests
{
    private static async Task<IDatabaseConnection> OpenSampleAsync()
    {
        var connection = await new SqliteDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "plan", DatabaseDialect.Sqlite, null, null, null, null, ":memory:", null), null, TestContext.Current.CancellationToken);
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", "sqlite.sql"), TestContext.Current.CancellationToken);
        await using var load = await connection.ExecuteAsync(script, [], TestContext.Current.CancellationToken);
        Assert.Null(await load.ReadBatchAsync(1, TestContext.Current.CancellationToken));
        return connection;
    }

    private static async Task AssertStillRunsAsync(IDatabaseConnection connection)
    {
        await using var reader = await connection.ExecuteAsync("select 7", [], TestContext.Current.CancellationToken);
        var batch = await reader.ReadBatchAsync(10, TestContext.Current.CancellationToken);
        Assert.NotNull(batch);
        Assert.Equal(7L, batch[0][0]);
    }

    [Fact]
    public async Task JoinGroupOrder_NamesTheRealTablesAndStillRunsAfterwards()
    {
        await using var connection = await OpenSampleAsync();
        var reader = Assert.IsAssignableFrom<IPlanReader>(connection);
        Assert.False(reader.SupportsActualPlan);
        var plan = await reader.ExplainAsync(
            "select customers.name, count(*) as n from orders join customers on customers.id = orders.customer_id where orders.total > @min group by customers.name order by n desc limit 5",
            [new QueryParameter("min", 1)], PlanMode.Estimated, TestContext.Current.CancellationToken);
        Assert.Equal(PlanMode.Estimated, plan.Mode);
        var targets = plan.Flatten().Select(n => n.Target).OfType<string>().ToList();
        Assert.Contains(targets, t => t.Contains("orders", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(targets, t => t.Contains("customers", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(plan.Flatten(), n => n.Kind == PlanNodeKind.Sort);
        await AssertStillRunsAsync(connection);
    }

    [Fact]
    public async Task Actual_IsUnavailable()
    {
        await using var connection = await OpenSampleAsync();
        var ex = await Assert.ThrowsAsync<PlanUnavailableException>(async () =>
            await ((IPlanReader)connection).ExplainAsync("select 1", [], PlanMode.Actual, TestContext.Current.CancellationToken));
        Assert.Equal("engine", ex.ReasonCode);
        await AssertStillRunsAsync(connection);
    }

    [Fact]
    public async Task SingleTableLookup_IsAnIndexSeekOnTheRealTable()
    {
        await using var connection = await OpenSampleAsync();
        var plan = await ((IPlanReader)connection).ExplainAsync("select * from customers where id = 1", [], PlanMode.Estimated, TestContext.Current.CancellationToken);
        Assert.Equal(PlanNodeKind.IndexSeek, plan.Root.Kind);
        Assert.StartsWith("customers", plan.Root.Target, StringComparison.Ordinal);
    }
}
