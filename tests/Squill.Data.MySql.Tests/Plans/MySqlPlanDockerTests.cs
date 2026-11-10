using Squill.Core;
using Squill.Core.Plans;
using Xunit;

namespace Squill.Data.MySql.Tests;

public sealed class MySqlPlanDockerTests(MySqlFixture server) : IClassFixture<MySqlFixture>
{
    private const string Join =
        "select c.name, sum(o.total) as t from orders o join customers c on c.id = o.customer_id where o.total > @min group by c.name order by t desc limit 5";

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
        Assert.False(reader.SupportsActualPlan);
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
}
