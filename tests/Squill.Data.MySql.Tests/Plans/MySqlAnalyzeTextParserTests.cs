using Squill.Core;
using Squill.Core.Plans;
using Xunit;

namespace Squill.Data.MySql.Tests;

public sealed class MySqlAnalyzeTextParserTests
{
    private static string Sample(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Plans", name + ".txt"));

    private static QueryPlan Load(string name) => MySqlAnalyzeTextParser.Parse("select 1", Sample(name));

    [Fact]
    public void JoinWithFilter_NestsByIndentAndMultipliesByLoops()
    {
        var plan = Load("analyze-join");
        Assert.Equal(DatabaseDialect.MySql, plan.Dialect);
        Assert.Equal(PlanMode.Actual, plan.Mode);
        Assert.Equal(PlanSourceFormat.Text, plan.SourceFormat);
        Assert.Equal(Sample("analyze-join"), plan.SourceText);
        Assert.False(plan.RolledBack);

        var join = plan.Root;
        Assert.Equal("Nested loop inner join", join.Operation);
        Assert.Equal(PlanNodeKind.Join, join.Kind);
        Assert.Equal(10967, join.EstimatedCost);
        Assert.Equal(16809, join.EstimatedRows);
        Assert.Equal(3876, join.ActualRows);
        Assert.Equal(9.45, join.ActualTimeMs);
        Assert.Equal(9.45, plan.ExecutionTimeMs);

        var filter = join.Children[0];
        Assert.Equal(PlanNodeKind.Filter, filter.Kind);
        Assert.Equal("Filter", filter.Operation);
        Assert.Equal("(o.total > 900.0000)", filter.Detail);
        var scan = Assert.Single(filter.Children);
        Assert.Equal(PlanNodeKind.TableScan, scan.Kind);
        Assert.Equal("Table scan", scan.Operation);
        Assert.Equal("o", scan.Target);
        Assert.Equal(50000, scan.ActualRows);

        var lookup = join.Children[1];
        Assert.Equal(PlanNodeKind.IndexSeek, lookup.Kind);
        Assert.Equal("Single-row index lookup", lookup.Operation);
        Assert.Equal("c", lookup.Target);
        Assert.Equal("using PRIMARY (id=o.customer_id)", lookup.Detail);
        Assert.Equal(3876, lookup.Loops);
        Assert.Equal(3876, lookup.ActualRows); // 1 row per loop x 3876 loops
        Assert.Equal(525e-6 * 3876, lookup.ActualTimeMs!.Value, 6);
        Assert.Equal(Enumerable.Range(0, 4), plan.Flatten().Select(n => n.Id));
    }

    [Fact]
    public void SortAndLimit_AreRecognized()
    {
        var plan = Load("analyze-sort-limit");
        Assert.Equal(PlanNodeKind.Limit, plan.Root.Kind);
        Assert.Equal("10 row(s)", plan.Root.Detail);
        var sort = Assert.Single(plan.Root.Children);
        Assert.Equal(PlanNodeKind.Sort, sort.Kind);
        Assert.StartsWith("orders.total DESC", sort.Detail, StringComparison.Ordinal);
        Assert.Equal(PlanNodeKind.TableScan, Assert.Single(sort.Children).Kind);
    }

    [Fact]
    public void Aggregate_UsingTemporaryTable_KeepsItsDetailAndHasNoCost()
    {
        var plan = Load("analyze-aggregate");
        Assert.Equal("Table scan", plan.Root.Operation);
        Assert.Equal("<temporary>", plan.Root.Target);
        var aggregate = Assert.Single(plan.Root.Children);
        Assert.Equal("Aggregate", aggregate.Operation);
        Assert.Equal(PlanNodeKind.Aggregate, aggregate.Kind);
        Assert.Equal("using temporary table", aggregate.Detail);
        Assert.Null(aggregate.EstimatedCost);
        Assert.Equal(2000, aggregate.ActualRows);
        Assert.Contains(plan.Flatten(), n => n.Kind == PlanNodeKind.IndexSeek && n.Target == "o" && n.Loops == 2000);
    }

    [Fact]
    public void NeverExecutedBranch_HasZeroActualFiguresAndAProperty()
    {
        var plan = Load("analyze-never");
        var never = plan.Flatten().Where(n => n.Properties.Any(p => p.Name == "Never executed")).ToList();
        Assert.Equal(2, never.Count);
        Assert.All(never, n =>
        {
            Assert.Equal(0, n.ActualRows);
            Assert.Equal(0, n.ActualTimeMs);
            Assert.Equal(0, n.Loops);
        });
        Assert.Equal(1982, plan.Root.EstimatedCost);
    }

    [Fact]
    public void JunkLine_BecomesAnOtherNodeKeepingItsText()
    {
        var plan = MySqlAnalyzeTextParser.Parse("select 1", "-> Filter: (a > 1)  (cost=1 rows=2) (actual time=0.1..0.2 rows=3 loops=1)\n    something unexpected\n");
        var junk = Assert.Single(plan.Root.Children);
        Assert.Equal(PlanNodeKind.Other, junk.Kind);
        Assert.Equal("something unexpected", junk.Operation);
    }

    [Fact]
    public void ZeroRowsLine_KeepsItsFirstCostFigure()
    {
        var plan = MySqlAnalyzeTextParser.Parse("select 1", "-> Zero rows (Impossible WHERE)  (cost=0..0 rows=0) (actual time=586e-6..586e-6 rows=0 loops=1)\n");
        Assert.Equal("Zero rows (Impossible WHERE)", plan.Root.Operation);
        Assert.Equal(0, plan.Root.EstimatedCost);
        Assert.Equal(586e-6, plan.Root.ActualTimeMs!.Value, 9);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \n\r\n")]
    public void EmptyOutput_RaisesPlanParseException(string text)
    {
        var ex = Assert.Throws<PlanParseException>(() => MySqlAnalyzeTextParser.Parse("select 1", text));
        Assert.Equal(text, ex.SourceText);
    }
}
