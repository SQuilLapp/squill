using Squill.Core;
using Squill.Core.Plans;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class PostgresPlanParserTests
{
    private static string Sample(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Plans", name + ".json"));

    private static QueryPlan Load(string name, PlanMode mode) => PostgresPlanParser.Parse("select 1", mode, Sample(name));

    private static string? Property(PlanNode node, string name) => node.Properties.FirstOrDefault(p => p.Name == name)?.Value;

    [Fact]
    public void EstimatedJoin_HasTheExpectedShapeAndNumbers()
    {
        var plan = Load("estimated-join", PlanMode.Estimated);
        Assert.Equal(DatabaseDialect.PostgreSql, plan.Dialect);
        Assert.Equal(PlanSourceFormat.Json, plan.SourceFormat);
        Assert.Equal(Sample("estimated-join"), plan.SourceText);
        Assert.Equal(["Limit", "Sort", "Aggregate", "Hash Join", "Seq Scan", "Hash", "Seq Scan"], plan.Flatten().Select(n => n.Operation));
        Assert.Equal(Enumerable.Range(0, 7), plan.Flatten().Select(n => n.Id));
        Assert.Equal(PlanNodeKind.Limit, plan.Root.Kind);
        Assert.NotNull(plan.Root.EstimatedCost);
        Assert.All(plan.Flatten().Where(n => n.Kind == PlanNodeKind.TableScan), n => Assert.NotNull(n.EstimatedRows));
        Assert.All(plan.Flatten(), n => Assert.Null(n.ActualRows));
        Assert.Null(plan.ExecutionTimeMs);
        Assert.Null(plan.BuffersReadBytes);
        Assert.False(plan.RolledBack);
        var join = Assert.Single(plan.Flatten(), n => n.Kind == PlanNodeKind.Join);
        Assert.Contains("customer_id", join.Detail, StringComparison.Ordinal);
        Assert.Equal("Inner", Property(join, "Join Type"));
        var scan = plan.Flatten().First(n => n.Kind == PlanNodeKind.TableScan);
        Assert.Equal("orders o", scan.Target);
    }

    [Fact]
    public void Hash_IsAHelperNode()
    {
        var hash = Assert.Single(Load("estimated-join", PlanMode.Estimated).Flatten(), n => n.Operation == "Hash");
        Assert.True(hash.IsHelper);
        Assert.Equal(PlanNodeKind.Helper, hash.Kind);
        Assert.Empty(hash.Warnings);
    }

    [Fact]
    public void AnalyzedJoin_GivesTimingsBuffersAndSortKey()
    {
        var plan = Load("analyzed-join", PlanMode.Actual);
        Assert.Equal(PlanMode.Actual, plan.Mode);
        Assert.NotNull(plan.PlanningTimeMs);
        Assert.True(plan.ExecutionTimeMs > 0);
        Assert.NotNull(plan.BuffersReadBytes);
        Assert.Equal(0, plan.BuffersReadBytes % 8192);
        Assert.All(plan.Flatten(), n => Assert.NotNull(n.ActualRows));
        var sort = Assert.Single(plan.Flatten(), n => n.Kind == PlanNodeKind.Sort);
        Assert.Contains("DESC", sort.Detail, StringComparison.Ordinal);
        Assert.Equal("top-N heapsort", Property(sort, "Sort Method"));
        Assert.Empty(sort.Warnings);
        Assert.Equal(1, plan.Root.Loops);
    }

    [Fact]
    public void ParallelPlan_MultipliesPerLoopFiguresByLoops()
    {
        var plan = Load("analyzed-parallel", PlanMode.Actual);
        var gather = Assert.Single(plan.Flatten(), n => n.Kind == PlanNodeKind.Parallel);
        Assert.NotNull(Property(gather, "Workers Planned"));
        var partial = Assert.Single(plan.Flatten(), n => n.Kind == PlanNodeKind.Aggregate && n.Loops == 3);
        Assert.Equal(3, partial.ActualRows); // one partial row per loop
        var scan = Assert.Single(plan.Flatten(), n => n.Kind == PlanNodeKind.TableScan);
        Assert.Equal(3, scan.Loops);
        Assert.True(scan.ActualRows > 1000);
        Assert.NotNull(Property(scan, "Rows Removed by Filter"));
    }

    [Fact]
    public void BitmapScan_UsesTheIndexSeekKindForTheIndexCondition()
    {
        var plan = Load("analyzed-bitmap", PlanMode.Actual);
        Assert.Equal(PlanNodeKind.IndexScan, plan.Root.Kind);
        var seek = Assert.Single(plan.Flatten(), n => n.Operation == "Bitmap Index Scan");
        Assert.Equal(PlanNodeKind.IndexSeek, seek.Kind);
        Assert.Contains("customer_id", seek.Detail, StringComparison.Ordinal);
        Assert.Contains("orders_customer_placed", seek.Target, StringComparison.Ordinal);
    }

    [Fact]
    public void SortOnDisk_RaisesSortSpill()
    {
        var sort = Assert.Single(Load("analyzed-sort-spill", PlanMode.Actual).Flatten(), n => n.Kind == PlanNodeKind.Sort);
        var warning = Assert.Single(sort.Warnings);
        Assert.Equal(PlanMessageCodes.SortSpill, warning.Code);
        Assert.Equal("external merge", warning.Args[0]);
        Assert.True(double.Parse(warning.Args[1], System.Globalization.CultureInfo.InvariantCulture) > 0);
    }

    [Fact]
    public void Subquery_SemiJoinPlanParses()
    {
        var plan = Load("estimated-subquery", PlanMode.Estimated);
        Assert.Contains(plan.Flatten(), n => n.Kind == PlanNodeKind.Join);
        Assert.Contains(plan.Flatten(), n => n.Target?.StartsWith("customers", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void HashBatchesAboveOne_RaiseHashSpill_AndUnknownNodeIsOther()
    {
        const string json = """[{"Plan":{"Node Type":"Hash","Hash Batches":4,"Peak Memory Usage":64,"Plans":[{"Node Type":"Quantum Scan","Brand New Key":["a","b"],"Plan Rows":3}]}}]""";
        var plan = PostgresPlanParser.Parse("select 1", PlanMode.Estimated, json);
        Assert.Equal(PlanMessageCodes.HashSpill, Assert.Single(plan.Root.Warnings).Code);
        var other = Assert.Single(plan.Root.Children);
        Assert.Equal(PlanNodeKind.Other, other.Kind);
        Assert.Equal("Quantum Scan", other.Operation);
        Assert.Equal("a, b", Property(other, "Brand New Key"));
        Assert.Equal(3, other.EstimatedRows);
    }

    [Fact]
    public void TruncatedJson_RaisesPlanParseExceptionWithTheSourceText()
    {
        var json = Sample("estimated-join")[..200];
        var ex = Assert.Throws<PlanParseException>(() => PostgresPlanParser.Parse("select 1", PlanMode.Estimated, json));
        Assert.Equal(json, ex.SourceText);
    }

    [Fact]
    public void DocumentWithoutPlan_RaisesPlanParseException() =>
        Assert.Throws<PlanParseException>(() => PostgresPlanParser.Parse("select 1", PlanMode.Estimated, """[{"Other":1}]"""));
}
