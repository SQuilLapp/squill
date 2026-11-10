using Squill.Core;
using Squill.Core.Plans;
using Xunit;

namespace Squill.Data.MySql.Tests;

public sealed class MySqlPlanParserTests
{
    private static string Sample(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Plans", name + ".json"));

    private static QueryPlan Load(string name) => MySqlPlanParser.Parse("select 1", Sample(name));

    private static string? Property(PlanNode node, string name) => node.Properties.FirstOrDefault(p => p.Name == name)?.Value;

    [Fact]
    public void SingleTable_IsOneScanUnderTheQueryRoot()
    {
        var plan = Load("single");
        Assert.Equal(DatabaseDialect.MySql, plan.Dialect);
        Assert.Equal(PlanMode.Estimated, plan.Mode);
        Assert.Equal(PlanSourceFormat.Json, plan.SourceFormat);
        Assert.Equal(Sample("single"), plan.SourceText);
        Assert.Equal("Query", plan.Root.Operation);
        Assert.Equal(PlanNodeKind.Result, plan.Root.Kind);
        Assert.NotNull(plan.Root.EstimatedCost);
        var scan = Assert.Single(plan.Root.Children);
        Assert.Equal("Single-row lookup", scan.Operation);
        Assert.Equal(PlanNodeKind.IndexSeek, scan.Kind);
        Assert.Equal("customers · PRIMARY", scan.Target);
        Assert.Equal("id", scan.Detail);
        Assert.Equal(1, scan.EstimatedRows);
        Assert.NotNull(scan.EstimatedCost);
        Assert.Null(scan.RowWidth);
        Assert.NotNull(Property(scan, "possible_keys"));
        Assert.NotNull(Property(scan, "read_cost"));
    }

    [Fact]
    public void TwoTables_GiveOneJoinWithConditionAndCosts()
    {
        var plan = Load("join2");
        var join = Assert.Single(plan.Root.Children);
        Assert.Equal("Nested Loop", join.Operation);
        Assert.Equal(PlanNodeKind.Join, join.Kind);
        var (outer, inner) = (join.Children[0], join.Children[1]);
        Assert.Equal(PlanNodeKind.TableScan, outer.Kind);
        Assert.Equal("Table scan", outer.Operation);
        Assert.Equal("o", outer.Target);
        Assert.Equal(50432, outer.EstimatedRows);
        Assert.Contains("total", outer.Detail, StringComparison.Ordinal);
        Assert.Equal("c · PRIMARY", inner.Target);
        Assert.Equal(inner.EstimatedCost, join.EstimatedCost);
        Assert.Equal(10966.59, plan.Root.EstimatedCost);
        Assert.Equal(5083.45, outer.EstimatedCost);
    }

    [Fact]
    public void ThreeTables_GiveALeftDeepChain()
    {
        var plan = Load("join3");
        var top = Assert.Single(plan.Root.Children);
        Assert.Equal(PlanNodeKind.Join, top.Kind);
        Assert.Equal(PlanNodeKind.Join, top.Children[0].Kind);
        Assert.NotEqual(PlanNodeKind.Join, top.Children[1].Kind);
        Assert.Equal(2, plan.Flatten().Count(n => n.Kind == PlanNodeKind.Join));
        Assert.Equal(3, plan.Flatten().Count(n => n.Target is not null));
        Assert.Equal(Enumerable.Range(0, plan.Flatten().Count()), plan.Flatten().Select(n => n.Id));
    }

    [Fact]
    public void OrderAndGroup_NestAsSortThenAggregateThenJoin()
    {
        var plan = Load("order-group");
        var sort = Assert.Single(plan.Root.Children);
        Assert.Equal(PlanNodeKind.Sort, sort.Kind);
        Assert.Equal("true", Property(sort, "using_filesort"), ignoreCase: true);
        var group = Assert.Single(sort.Children);
        Assert.Equal(PlanNodeKind.Aggregate, group.Kind);
        Assert.Equal("true", Property(group, "using_temporary_table"), ignoreCase: true);
        Assert.Equal(PlanNodeKind.Join, Assert.Single(group.Children).Kind);
    }

    [Fact]
    public void Union_HasOneChildPerQuerySpecification()
    {
        var union = Assert.Single(Load("union").Root.Children);
        Assert.Equal(PlanNodeKind.Union, union.Kind);
        Assert.Equal(2, union.Children.Count);
        Assert.All(union.Children, c => Assert.Equal(PlanNodeKind.IndexScan, c.Kind));
        Assert.Equal(["customers · email", "orders · orders_customer_placed"], union.Children.Select(c => c.Target));
    }

    [Fact]
    public void SubqueryInWhere_IsAMaterializedSubqueryUnderItsTable()
    {
        var plan = Load("subquery");
        var sub = Assert.Single(plan.Flatten(), n => n.Kind == PlanNodeKind.Subquery);
        Assert.Equal("Materialized subquery", sub.Operation);
        Assert.Equal("orders", Assert.Single(sub.Children).Target);
    }

    [Fact]
    public void DerivedTable_NestsItsOwnGroupingBlock()
    {
        var plan = Load("derived");
        var derived = Assert.Single(plan.Root.Children);
        Assert.Equal("d", derived.Target);
        var sub = Assert.Single(derived.Children);
        Assert.Equal(PlanNodeKind.Subquery, sub.Kind);
        Assert.Equal(PlanNodeKind.Aggregate, Assert.Single(sub.Children).Kind);
    }

    [Fact]
    public void NumericStringsAndUnknownKeys_Survive()
    {
        const string json = """
            {"query_block":{"select_id":1,"cost_info":{"query_cost":"12.50"},"future_key":{"a":1},
             "table":{"table_name":"t","access_type":"ALL","rows_examined_per_scan":7,"cost_info":{"prefix_cost":"oops","read_cost":"1.00"},"brand_new":["x","y"]}}}
            """;
        var plan = MySqlPlanParser.Parse("select 1", json);
        Assert.Equal(12.5, plan.Root.EstimatedCost);
        Assert.Equal("""{"a":1}""", Property(plan.Root, "future_key"));
        var scan = Assert.Single(plan.Root.Children);
        Assert.Null(scan.EstimatedCost);
        Assert.Equal("x, y", Property(scan, "brand_new"));
        Assert.Equal("1.00", Property(scan, "read_cost"));
    }

    [Fact]
    public void WithoutTables_TheRootHasNoChildren()
    {
        var plan = MySqlPlanParser.Parse("select 1", """{"query_block":{"select_id":1,"message":"No tables used"}}""");
        Assert.Empty(plan.Root.Children);
        Assert.Equal("No tables used", Property(plan.Root, "message"));
    }

    [Fact]
    public void TruncatedJson_RaisesPlanParseExceptionWithTheSourceText()
    {
        var json = Sample("join2")[..120];
        var ex = Assert.Throws<PlanParseException>(() => MySqlPlanParser.Parse("select 1", json));
        Assert.Equal(json, ex.SourceText);
    }

    [Fact]
    public void DocumentWithoutQueryBlock_RaisesPlanParseException() =>
        Assert.Throws<PlanParseException>(() => MySqlPlanParser.Parse("select 1", """{"x":1}"""));
}
