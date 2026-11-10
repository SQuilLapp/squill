using System.Globalization;
using Squill.Core.Plans;
using Xunit;

namespace Squill.Data.Sqlite.Tests;

public sealed class SqlitePlanParserTests
{
    private static QueryPlan Load(string name)
    {
        var rows = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Plans", name + ".txt"))
            .Where(l => l.Length > 0)
            .Select(l => l.Split('\t'))
            .Select(f => new SqlitePlanRow(long.Parse(f[0], CultureInfo.InvariantCulture), long.Parse(f[1], CultureInfo.InvariantCulture), f[3]))
            .ToList();
        return SqlitePlanParser.Parse("select 1", rows);
    }

    [Fact]
    public void TableScan_HasNoIndexWarningAndNoCost()
    {
        var plan = Load("scan");
        Assert.Equal(PlanNodeKind.TableScan, plan.Root.Kind);
        Assert.Equal("SCAN", plan.Root.Operation);
        Assert.Equal("no_pk", plan.Root.Target);
        Assert.Equal(PlanMessageCodes.NoIndex, Assert.Single(plan.Root.Warnings).Code);
        Assert.Null(plan.Root.EstimatedCost);
        Assert.Null(plan.Root.EstimatedRows);
        Assert.Null(plan.ExecutionTimeMs);
        Assert.False(plan.RolledBack);
        Assert.Equal(PlanSourceFormat.Text, plan.SourceFormat);
        Assert.Equal("QUERY PLAN\n`--SCAN no_pk", plan.SourceText);
    }

    [Fact]
    public void ConstantRow_IsAScanWithoutWarning()
    {
        var root = Load("constant").Root;
        Assert.Equal(PlanNodeKind.TableScan, root.Kind);
        Assert.Empty(root.Warnings);
    }

    [Fact]
    public void SearchByIndex_IsAnIndexSeekWithCondition()
    {
        var root = Load("search-index").Root;
        Assert.Equal(PlanNodeKind.IndexSeek, root.Kind);
        Assert.Equal("customers · sqlite_autoindex_customers_1", root.Target);
        Assert.Equal("email=?", root.Detail);
    }

    [Fact]
    public void SearchByRowid_NamesThePrimaryKey()
    {
        var root = Load("search-rowid").Root;
        Assert.Equal(PlanNodeKind.IndexSeek, root.Kind);
        Assert.Equal("customers · INTEGER PRIMARY KEY", root.Target);
        Assert.Equal("rowid=?", root.Detail);
    }

    [Fact]
    public void Join_GivesASyntheticRootWithTheStepsAsSiblings()
    {
        var plan = Load("join-group-order");
        Assert.Equal("Query", plan.Root.Operation);
        Assert.Equal(PlanNodeKind.Result, plan.Root.Kind);
        Assert.Equal(["SCAN", "SEARCH", "USE TEMP B-TREE", "USE TEMP B-TREE"], plan.Root.Children.Select(c => c.Operation));
        Assert.Equal([PlanNodeKind.Aggregate, PlanNodeKind.Sort], plan.Root.Children.Skip(2).Select(c => c.Kind));
        Assert.Equal(Enumerable.Range(0, 5), plan.Flatten().Select(n => n.Id));
    }

    [Fact]
    public void OrderBy_IsASort()
    {
        var plan = Load("order-by");
        Assert.Contains(plan.Flatten(), n => n.Kind == PlanNodeKind.Sort && n.Detail == "FOR ORDER BY");
    }

    [Fact]
    public void Subquery_NestsItsCoveringIndexScan()
    {
        var plan = Load("subquery");
        var sub = Assert.Single(plan.Flatten(), n => n.Kind == PlanNodeKind.Subquery);
        Assert.Equal("LIST SUBQUERY", sub.Operation);
        var scan = Assert.Single(sub.Children);
        Assert.Equal(PlanNodeKind.IndexScan, scan.Kind);
        Assert.Empty(scan.Warnings);
    }

    [Fact]
    public void Union_IsAUnionWithBothSides()
    {
        var plan = Load("union");
        Assert.Equal(PlanNodeKind.Union, plan.Root.Kind);
        Assert.Equal(2, plan.Flatten().Count(n => n.Kind == PlanNodeKind.TableScan));
    }

    [Fact]
    public void LeftJoinSearch_KeepsTheConditionAndFlagsTheJoin()
    {
        var plan = Load("left-join-view");
        var seek = Assert.Single(plan.Flatten(), n => n.Kind == PlanNodeKind.IndexSeek);
        Assert.Equal("o · orders_customer_placed", seek.Target);
        Assert.Equal("customer_id=? LEFT-JOIN", seek.Detail);
        Assert.Contains(plan.Flatten(), n => n.Kind == PlanNodeKind.Subquery && n.Operation == "CO-ROUTINE");
    }

    [Fact]
    public void UnknownText_BecomesAnOtherNode()
    {
        var root = Load("unknown").Root;
        Assert.Equal(PlanNodeKind.Other, root.Kind);
        Assert.Equal("SOMETHING NEW", root.Operation);
    }
}
