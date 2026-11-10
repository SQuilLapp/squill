using Squill.Core;
using Squill.Core.Plans;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

public sealed class SqlServerPlanDockerTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    private const string Join =
        "select top 5 c.name, sum(o.total) as t from sample.orders o join sample.customers c on c.id = o.customer_id where o.total > 0 group by c.name order by t desc";

    private async Task<IDatabaseConnection> OpenAsync()
    {
        var connection = await new SqlServerDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "plan", DatabaseDialect.SqlServer, server.Host, server.Port, SqlServerFixture.Database, SqlServerFixture.UserName, null, null),
            SqlServerFixture.Password,
            CancellationToken.None);
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "SampleSchema", "sqlserver.sql"), TestContext.Current.CancellationToken);
        foreach (var batch in script.Split('\n').Aggregate(new List<string> { "" }, (list, line) =>
        {
            if (line.Trim() == "GO")
                list.Add("");
            else
                list[^1] += line + "\n";
            return list;
        }))
        {
            await using var reader = await connection.ExecuteAsync(batch, [], CancellationToken.None);
            Assert.Null(await reader.ReadBatchAsync(1, CancellationToken.None));
        }

        return connection;
    }

    // In showplan mode this statement would return the plan XML instead of the number.
    private static async Task AssertStillRunsAsync(IDatabaseConnection connection)
    {
        await using var reader = await connection.ExecuteAsync("select 1 as one", [], CancellationToken.None);
        var rows = await reader.ReadAllAsync(10, CancellationToken.None);
        Assert.Equal(1, Convert.ToInt32(Assert.Single(rows)[0], System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Estimated_PlanOfTheSampleJoin_AndNormalStatementsStillRun()
    {
        await using var connection = await OpenAsync();
        var reader = (IPlanReader)connection;
        Assert.False(reader.SupportsActualPlan);
        var plan = await reader.ExplainAsync(Join, [], PlanMode.Estimated, TestContext.Current.CancellationToken);
        Assert.Equal(PlanMode.Estimated, plan.Mode);
        Assert.NotNull(plan.Root.EstimatedCost);
        Assert.Contains(plan.Flatten(), n => n.Kind == PlanNodeKind.Join);
        Assert.Contains(plan.Flatten(), n => n.Kind == PlanNodeKind.Sort);
        var targets = plan.Flatten().Select(n => n.Target).OfType<string>().ToList();
        Assert.Contains(targets, t => t.Contains("[orders]", StringComparison.Ordinal));
        Assert.Contains(targets, t => t.Contains("[customers]", StringComparison.Ordinal));
        await AssertStillRunsAsync(connection);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Estimated_DoesNotExecuteTheStatement()
    {
        await using var connection = await OpenAsync();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var plan = await ((IPlanReader)connection).ExplainAsync("select name from sys.objects; waitfor delay '00:00:30'", [], PlanMode.Estimated, TestContext.Current.CancellationToken);
        Assert.NotNull(plan.Root);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(20));
        await AssertStillRunsAsync(connection);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Estimated_WithBoundParameters_IsUnavailable_AndNormalStatementsStillRun()
    {
        await using var connection = await OpenAsync();
        var ex = await Assert.ThrowsAsync<PlanUnavailableException>(async () =>
            await ((IPlanReader)connection).ExplainAsync("select id from sample.orders where total > @min", [new QueryParameter("min", 0m)], PlanMode.Estimated, TestContext.Current.CancellationToken));
        Assert.Equal("parameters", ex.ReasonCode);
        await AssertStillRunsAsync(connection);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Estimated_OfInvalidSql_Throws_AndNormalStatementsStillRun()
    {
        await using var connection = await OpenAsync();
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await ((IPlanReader)connection).ExplainAsync("select from from", [], PlanMode.Estimated, TestContext.Current.CancellationToken));
        await AssertStillRunsAsync(connection);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Estimated_Cancelled_Throws_AndNormalStatementsStillRun()
    {
        await using var connection = await OpenAsync();
        using var already = new CancellationTokenSource();
        await already.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await ((IPlanReader)connection).ExplainAsync("select 1", [], PlanMode.Estimated, already.Token));
        await AssertStillRunsAsync(connection);

        // Cancelled while the statement is compiling: a huge IN list takes the optimizer long enough.
        var huge = "select count(*) from sample.orders where id in (" + string.Join(',', Enumerable.Range(1, 20000)) + ")";
        using var soon = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        try
        {
            await ((IPlanReader)connection).ExplainAsync(huge, [], PlanMode.Estimated, soon.Token);
        }
        catch (OperationCanceledException)
        {
            // Either outcome is fine: the point is the state of the connection afterwards.
        }

        await AssertStillRunsAsync(connection);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Actual_IsNotAvailableYet()
    {
        await using var connection = await OpenAsync();
        var ex = await Assert.ThrowsAsync<PlanUnavailableException>(async () =>
            await ((IPlanReader)connection).ExplainAsync("select 1", [], PlanMode.Actual, TestContext.Current.CancellationToken));
        Assert.Equal("engine", ex.ReasonCode);
        await AssertStillRunsAsync(connection);
    }
}
