using Squill.Core;
using Squill.Core.Plans;
using Xunit;

namespace Squill.Data.MySql.Tests;

public sealed class MariaDbPlanParserTests
{
    private static string Sample(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Plans", "mariadb-" + name + ".json"));

    private static QueryPlan Load(string name, PlanMode mode = PlanMode.Estimated) => MariaDbPlanParser.Parse("select 1", Sample(name), mode);

    [Fact]
    public void SingleLookup_IsOneIndexSeek_WithNoInventedCost()
    {
        var plan = Load("single");
        Assert.Equal(DatabaseDialect.MySql, plan.Dialect);
        Assert.Equal(PlanSourceFormat.Json, plan.SourceFormat);
        Assert.Equal(Sample("single"), plan.SourceText);
        Assert.Null(plan.Root.EstimatedCost);
        var scan = Assert.Single(plan.Root.Children);
        Assert.Equal("Single-row lookup", scan.Operation);
        Assert.Equal("customers · PRIMARY", scan.Target);
        Assert.Equal("id", scan.Detail);
        Assert.Equal(1, scan.EstimatedRows);
        Assert.Null(scan.EstimatedCost);
        Assert.Null(scan.ActualRows);
        Assert.Null(plan.ExecutionTimeMs);
    }

    [Fact]
    public void JoinWithSortAndGroup_GivesSortOverTemporaryTableOverJoin()
    {
        var plan = Load("join");
        Assert.Equal(0.01378888, plan.Root.EstimatedCost);
        var sort = Assert.Single(plan.Root.Children);
        Assert.Equal(PlanNodeKind.Sort, sort.Kind);
        Assert.Equal("sum(o.total) desc", sort.Properties.Single(p => p.Name == "sort_key").Value);
        var join = Assert.Single(Assert.Single(sort.Children).Children);
        Assert.Equal(PlanNodeKind.Join, join.Kind);
        var (first, second) = (join.Children[0], join.Children[1]);
        Assert.Equal(("Table scan", "o", "o.total > 0", 2d), (first.Operation, first.Target, first.Detail, first.EstimatedRows));
        Assert.Equal(0.0111808, first.EstimatedCost);
        Assert.Equal(("Index lookup", "c · PRIMARY"), (second.Operation, second.Target));
        Assert.Contains(second.Properties, p => p is { Name: "ref", Value: "squill.o.customer_id" });
        Assert.DoesNotContain(plan.Flatten(), n => n.ActualRows is not null);
    }

    [Fact]
    public void Analyze_TotalsRowsAndTimeOverTheLoops()
    {
        var plan = Load("analyze-join", PlanMode.Actual);
        Assert.Equal(PlanMode.Actual, plan.Mode);
        Assert.Equal(0.089633977, plan.ExecutionTimeMs);
        Assert.Equal(0.129079123, plan.PlanningTimeMs);
        var tables = plan.Flatten().Where(n => n.Kind is PlanNodeKind.TableScan or PlanNodeKind.IndexSeek).ToList();
        var scan = Assert.Single(tables, n => n.Target == "o");
        Assert.Equal(2, scan.ActualRows);
        Assert.Equal(1, scan.Loops);
        Assert.Equal(0.015580949 + 0.013104062, scan.ActualTimeMs);
        // r_rows is a per-loop average: 1 row over 2 loops is 2 rows.
        var lookup = Assert.Single(tables, n => n.Target is "c · PRIMARY");
        Assert.Equal(2, lookup.ActualRows);
        Assert.Equal(2, lookup.Loops);
        var sort = plan.Flatten().Single(n => n.Kind == PlanNodeKind.Sort);
        Assert.Equal(2, sort.ActualRows);
    }

    [Fact]
    public void Analyze_OfAConstLookupThatReadNothing_HasNullActualRows()
    {
        var scan = Assert.Single(Load("analyze-single", PlanMode.Actual).Root.Children);
        Assert.Null(scan.ActualRows);
        Assert.Equal(0, scan.Loops);
    }

    [Fact]
    public void Union_HasOneChildPerQueryBlock()
    {
        var union = Assert.Single(Load("union").Root.Children);
        Assert.Equal(PlanNodeKind.Union, union.Kind);
        Assert.Equal(2, union.Children.Count);
        Assert.All(union.Children, c => Assert.Equal("index", Assert.Single(c.Children).Properties.Count(p => p.Name == "using_index") == 1 ? "index" : "?"));
    }

    [Fact]
    public void CorrelatedSubqueries_AndBlockNestedLoop_AreKept()
    {
        var plan = Load("correlated");
        var nodes = plan.Flatten().ToList();
        Assert.Equal(2, nodes.Count(n => n.Kind == PlanNodeKind.Subquery));
        Assert.Contains(nodes, n => n is { Operation: "Block nested loop", Kind: PlanNodeKind.Join, Detail: "customers.`name` = c.`name`" });
        Assert.Contains(nodes, n => n.Target == "orders" && n.Detail == "orders.total > c.`id`");
        Assert.Equal(Enumerable.Range(0, nodes.Count), nodes.Select(n => n.Id));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"other\": 1}")]
    public void NotAPlanDocument_ThrowsPlanParseException(string text) =>
        Assert.Throws<PlanParseException>(() => MariaDbPlanParser.Parse("select 1", text, PlanMode.Estimated));

    [Fact]
    public void UnknownKeys_AreIgnored() =>
        Assert.Empty(MariaDbPlanParser.Parse("select 1", "{\"query_block\":{\"select_id\":1,\"future\":{\"x\":1},\"message\":\"No tables used\"}}", PlanMode.Estimated).Root.Children);
}
