using Xunit;
using Squill.Core.Plans;
using Squill.Data.Oracle;

namespace Squill.Data.Oracle.Tests;

public sealed class OraclePlanParserTests
{
    private static OraclePlanRow Row(long id, long? parent, string operation, string? options = null, string? name = null) =>
        new(id, parent, operation, options, name is null ? null : "HR", name, 10, 3, 200, null, null);

    [Fact]
    public void Parse_BuildsTheTreeFromParentIds()
    {
        var plan = OraclePlanParser.Parse("select 1", [
            Row(0, null, "SELECT STATEMENT"),
            Row(1, 0, "HASH JOIN"),
            Row(2, 1, "TABLE ACCESS", "FULL", "EMP"),
            Row(3, 1, "INDEX", "UNIQUE SCAN", "DEPT_PK")], null);

        Assert.Equal(PlanNodeKind.Result, plan.Root.Kind);
        var join = Assert.Single(plan.Root.Children);
        Assert.Equal(PlanNodeKind.Join, join.Kind);
        Assert.Equal([PlanNodeKind.TableScan, PlanNodeKind.IndexSeek], join.Children.Select(c => c.Kind));
        Assert.Equal("HR.EMP", join.Children[0].Target);
        Assert.Equal(4, plan.Flatten().Count());
        Assert.Contains("HASH JOIN", plan.SourceText);
    }

    [Fact]
    public void Parse_KeepsTheDbmsXplanTextWhenGiven()
    {
        var plan = OraclePlanParser.Parse("select 1", [Row(0, null, "SELECT STATEMENT")], "Plan hash value: 1");

        Assert.Equal("Plan hash value: 1", plan.SourceText);
    }
}
