using System.Diagnostics;
using Squill.Core;
using Squill.Core.Plans;
using Squill.Data;
using Squill.Sql.Plans;
using Xunit;

namespace Squill.Data.RoundTrip;

/// <summary>What differs between engines in <see cref="PlanScenario"/>: SQL spelling and the guarantees the engine gives.</summary>
/// <param name="Dialect">The engine.</param>
/// <param name="Prefix">Schema prefix of the sample tables, with its dot (<c>sample.</c>), or empty.</param>
/// <param name="TopSyntax">The engine limits rows with <c>select top 5</c> instead of <c>limit 5</c>.</param>
/// <param name="SlowSql">A read-only statement that runs long enough to cancel (not <c>sleep()</c> on MySQL: KILL QUERY ends it without an error).</param>
/// <param name="HasCost">The plans carry costs and estimated rows (SQLite's do not).</param>
/// <param name="HasActual">The engine gives actual plans.</param>
/// <param name="RolledBackOnActual">Expected <see cref="QueryPlan.RolledBack"/> of an actual plan.</param>
/// <param name="ActualFoldsConstantLookup">An actual plan of a primary-key lookup names no table (MySQL prints <c>Rows fetched before execution</c>).</param>
/// <param name="HasJoinNode">The plan of a two-table join has a <see cref="PlanNodeKind.Join"/> node (SQLite lists the nested loops as plain steps: exception as data).</param>
internal sealed record PlanSpec(
    DatabaseDialect Dialect, string Prefix, bool TopSyntax, string SlowSql, bool HasCost, bool HasActual, bool RolledBackOnActual, bool HasJoinNode = true, bool ActualFoldsConstantLookup = false);

/// <summary>Checks that a provider gives a usable <see cref="QueryPlan"/> for the same statements on the sample schema; shared by every driver test project.</summary>
internal static class PlanScenario
{
    private static readonly TimeSpan CancelLimit = TimeSpan.FromSeconds(10);

    /// <summary>Puts a few rows in the sample tables so that every engine plans against non-empty tables.</summary>
    public static async Task SeedAsync(IDatabaseConnection connection, PlanSpec spec)
    {
        await ExecAsync(connection, $"insert into {spec.Prefix}customers (name, email) values ('a', 'a@x'), ('b', 'b@x'), ('c', 'c@x')");
        await ExecAsync(connection, $"insert into {spec.Prefix}orders (customer_id, total) select id, 5 from {spec.Prefix}customers");
    }

    /// <summary>Runs the estimated checks on three statements, then the actual checks and the cancel check when the engine supports them.</summary>
    public static async Task RunAsync(IDatabaseConnection connection, PlanSpec spec)
    {
        var reader = Assert.IsAssignableFrom<IPlanReader>(connection);
        Assert.Equal(spec.HasActual, reader.SupportsActualPlan);
        var p = spec.Prefix;
        var join = spec.TopSyntax
            ? $"select top 5 customers.name, sum(orders.total) as t from {p}orders join {p}customers on customers.id = orders.customer_id where orders.total > 3 group by customers.name order by t desc"
            : $"select customers.name, sum(orders.total) as t from {p}orders join {p}customers on customers.id = orders.customer_id where orders.total > 3 group by customers.name order by t desc limit 5";
        var lookup = $"select name from {p}customers where id = 1";
        var subquery = $"select name from {p}customers where id in (select customer_id from {p}orders where total > 3)";
        (string Sql, string[] Tables)[] statements = [(join, ["orders", "customers"]), (lookup, ["customers"]), (subquery, ["customers", "orders"])];

        foreach (var (sql, tables) in statements)
        {
            var estimated = await reader.ExplainAsync(sql, [], PlanMode.Estimated, CancellationToken.None);
            AssertPlan(estimated, spec, tables, PlanMode.Estimated);
            if (ReferenceEquals(sql, join))
            {
                if (spec.HasJoinNode)
                    Assert.Contains(estimated.Flatten(), n => n.Kind == PlanNodeKind.Join);
                Assert.Contains(estimated.Flatten(), n => n.Kind == PlanNodeKind.Sort);
            }

            await AssertUsableAsync(connection);
        }

        // The reader is only called after PlanEligibility, which must refuse to analyze (execute) a write.
        Assert.False(PlanEligibility.Evaluate($"delete from {p}customers where id = 1", spec.Dialect).CanAnalyze);
        if (!spec.HasActual)
            return;

        foreach (var (sql, tables) in statements)
        {
            var actual = await reader.ExplainAsync(sql, [], PlanMode.Actual, CancellationToken.None);
            AssertPlan(actual, spec, spec.ActualFoldsConstantLookup && sql == lookup ? [] : tables, PlanMode.Actual);
            Assert.Equal(spec.RolledBackOnActual, actual.RolledBack);
            Assert.All(
                actual.Flatten().Where(n => n.Kind is PlanNodeKind.TableScan or PlanNodeKind.IndexScan or PlanNodeKind.IndexSeek && n.Target is not null && n.Loops != 0),
                n => Assert.NotNull(n.ActualRows));
            await AssertUsableAsync(connection);
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var clock = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await reader.ExplainAsync(spec.SlowSql, [], PlanMode.Actual, cts.Token));
        Assert.True(clock.Elapsed < CancelLimit, $"Cancelling the analyze took {clock.Elapsed}.");
        await AssertUsableAsync(connection);
    }

    private static void AssertPlan(QueryPlan plan, PlanSpec spec, string[] tables, PlanMode mode)
    {
        Assert.Equal(mode, plan.Mode);
        Assert.Equal(spec.Dialect, plan.Dialect);
        Assert.NotNull(plan.Root);
        var nodes = plan.Flatten().ToList();
        Assert.Equal(Enumerable.Range(0, nodes.Count), nodes.Select(n => n.Id));
        var targets = nodes.Select(n => n.Target).OfType<string>().ToList();
        foreach (var table in tables)
            Assert.True(targets.Any(t => t.Contains(table, StringComparison.OrdinalIgnoreCase)), $"{plan.Mode}: no node on {table}: {plan.SourceText}");

        // <temporary> is MySQL's internal grouping table, scanned without an estimate in an EXPLAIN ANALYZE tree.
        var scans = nodes.Where(n => n.Kind is PlanNodeKind.TableScan or PlanNodeKind.IndexScan or PlanNodeKind.IndexSeek && n.Target is not null && !n.Target.StartsWith('<')).ToList();
        if (spec.HasCost)
        {
            // MySQL's EXPLAIN ANALYZE tree gives no estimate on Limit/Sort nodes, so the root cost is only required of estimated plans.
            if (mode == PlanMode.Estimated)
                Assert.True(plan.Root.EstimatedCost is not null, $"Root {plan.Root.Operation} has no cost: {plan.SourceText}");
            Assert.All(scans, n => Assert.True(n.EstimatedRows is not null, $"{plan.Mode} {n.Operation} {n.Target} has no estimated rows"));
        }
        else
        {
            Assert.Null(plan.Root.EstimatedCost);
            Assert.All(nodes, n => Assert.Null(n.EstimatedRows));
        }

        var analysis = PlanAnalysis.Analyze(plan);
        Assert.All(analysis.Nodes.Values, s => Assert.True(s.RelativeShare is null or >= 0 and <= 1));
    }

    private static async Task ExecAsync(IDatabaseConnection connection, string sql)
    {
        await using var reader = await connection.ExecuteAsync(sql, [], CancellationToken.None);
        await reader.ReadAllAsync(10, CancellationToken.None);
    }

    private static async Task AssertUsableAsync(IDatabaseConnection connection)
    {
        await using var reader = await connection.ExecuteAsync("select 1", [], CancellationToken.None);
        var rows = await reader.ReadAllAsync(10, CancellationToken.None);
        Assert.Equal(1L, Convert.ToInt64(Assert.Single(rows)[0], System.Globalization.CultureInfo.InvariantCulture));
    }
}
