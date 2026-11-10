using Squill.Core.Plans;
using Xunit;

namespace Squill.Core.Tests.Plans;

public sealed class PlanModelTests
{
    private static PlanNode Node(string op, params PlanNode[] children) =>
        new(0, op, PlanNodeKind.Other, null, null, null, null, null, null, null, null, null, [], [], false, children);

    private static QueryPlan Plan(PlanNode root) =>
        new(DatabaseDialect.PostgreSql, PlanMode.Estimated, "select 1", root, [], [], null, null, null, false, PlanSourceFormat.Json, "{}");

    private static PlanNode Tree() => Node("A", Node("B", Node("D"), Node("E")), Node("C", Node("F")));

    [Fact]
    public void Assign_ThreeLevelTree_NumbersPreOrder()
    {
        var plan = Plan(PlanIds.Assign(Tree()));

        Assert.Equal(["A", "B", "D", "E", "C", "F"], plan.Flatten().Select(n => n.Operation));
        Assert.Equal([0, 1, 2, 3, 4, 5], plan.Flatten().Select(n => n.Id));
    }

    [Fact]
    public void Assign_DoesNotMutateInput()
    {
        var original = Tree();

        var assigned = PlanIds.Assign(original);

        Assert.NotSame(original, assigned);
        Assert.All(Plan(original).Flatten(), n => Assert.Equal(0, n.Id));
    }

    [Fact]
    public void Find_HitAndMiss()
    {
        var plan = Plan(PlanIds.Assign(Tree()));

        Assert.Equal("E", plan.Find(3)?.Operation);
        Assert.Null(plan.Find(99));
    }
}
