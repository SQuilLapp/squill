using Squill.Core.Plans;
using Xunit;

namespace Squill.Core.Tests.Plans;

public sealed class PlanAnalysisTests
{
    private static PlanNode Node(
        PlanNodeKind kind = PlanNodeKind.Other,
        double? cost = null,
        double? time = null,
        double? est = null,
        double? act = null,
        long? loops = null,
        PlanWarning[]? warnings = null,
        params PlanNode[] children) =>
        new(0, "Op", kind, null, null, est, cost, null, null, act, time, loops, [], warnings ?? [], false, children);

    private static QueryPlan Plan(PlanNode root, PlanMode mode = PlanMode.Actual, params PlanWarning[] planWarnings) =>
        new(DatabaseDialect.PostgreSql, mode, "select 1", PlanIds.Assign(root), planWarnings, [], null, null, null, false, PlanSourceFormat.Json, "{}");

    [Fact]
    public void Analyze_SharesSumToAtMostOneAndUseExclusiveCost()
    {
        var plan = Plan(Node(cost: 100, children: [Node(cost: 70, children: [Node(cost: 20)]), Node(cost: 10)]), PlanMode.Estimated);

        var r = PlanAnalysis.Analyze(plan);

        Assert.Equal(20, r.Nodes[0].ExclusiveCost);
        Assert.Equal(50, r.Nodes[1].ExclusiveCost);
        Assert.Equal(0.5, r.Nodes[1].RelativeShare);
        Assert.True(r.Nodes.Values.Sum(s => s.RelativeShare ?? 0) <= 1.0000001);
    }

    [Fact]
    public void Analyze_TimePreferredOverCost()
    {
        var plan = Plan(Node(cost: 100, time: 10, children: [Node(cost: 10, time: 9)]));

        var r = PlanAnalysis.Analyze(plan);

        Assert.Equal(0.9, r.Nodes[1].RelativeShare!.Value, 6);
        Assert.Equal(1, r.Nodes[0].ExclusiveTimeMs);
    }

    [Fact]
    public void Analyze_NoTimingNoCost_NullSharesNormalExceptProviderWarnings()
    {
        var warn = new PlanWarning(PlanMessageCodes.NoIndex, []);
        var plan = Plan(Node(children: [Node(warnings: [warn])]), PlanMode.Estimated);

        var r = PlanAnalysis.Analyze(plan);

        Assert.Null(r.Nodes[0].RelativeShare);
        Assert.Equal(PlanSeverity.Normal, r.Nodes[0].Severity);
        Assert.Equal(PlanSeverity.Alert, r.Nodes[1].Severity);
        Assert.Equal(1, r.AlertCount);
        Assert.Equal(1, r.WarningCount);
    }

    [Fact]
    public void Analyze_ZeroRootTotal_NullShare()
    {
        var r = PlanAnalysis.Analyze(Plan(Node(cost: 0), PlanMode.Estimated));

        Assert.Null(r.Nodes[0].RelativeShare);
    }

    [Theory]
    [InlineData(100, 990, false)]
    [InlineData(100, 1000, true)]
    [InlineData(1, 590, true)]
    [InlineData(0, 10, true)]
    [InlineData(0, 9, false)]
    public void Analyze_MisestimateUnder(double est, double act, bool expected)
    {
        var r = PlanAnalysis.Analyze(Plan(Node(est: est, act: act, loops: 1)));

        Assert.Equal(expected, r.Nodes[0].Warnings.Any(w => w.Code == PlanMessageCodes.Misestimate));
        Assert.Equal(expected, r.Nodes[0].MisestimateFactor is not null);
    }

    [Fact]
    public void Analyze_MisestimateOver_AndFactorArgs()
    {
        var r = PlanAnalysis.Analyze(Plan(Node(est: 5900, act: 10, loops: 1)));

        var w = Assert.Single(r.Nodes[0].Warnings);
        Assert.Equal(["590", "over"], w.Args);
        Assert.Equal(590, r.Nodes[0].MisestimateFactor);
    }

    [Fact]
    public void Analyze_MisestimateUsesRowsPerLoop_AndOnlyInActualMode()
    {
        Assert.Empty(PlanAnalysis.Analyze(Plan(Node(est: 10, act: 1000, loops: 100))).Nodes[0].Warnings);
        Assert.Empty(PlanAnalysis.Analyze(Plan(Node(est: 1, act: 1000, loops: 1), PlanMode.Estimated)).Nodes[0].Warnings);
    }

    [Fact]
    public void Analyze_FullScan_OnlyAboveThresholdAndNotUnderLimit()
    {
        var plan = Plan(Node(children:
        [
            Node(PlanNodeKind.TableScan, est: 9999),
            Node(PlanNodeKind.TableScan, est: 10000),
            Node(PlanNodeKind.Limit, children: [Node(PlanNodeKind.TableScan, est: 50000)]),
            Node(PlanNodeKind.TableScan),
        ]), PlanMode.Estimated);

        var r = PlanAnalysis.Analyze(plan);

        Assert.Empty(r.Nodes[1].Warnings);
        Assert.Equal(PlanMessageCodes.FullScan, Assert.Single(r.Nodes[2].Warnings).Code);
        Assert.Empty(r.Nodes[4].Warnings);
        Assert.Empty(r.Nodes[5].Warnings);
    }

    [Fact]
    public void Analyze_ChildrenExceedingParent_ClampsToZero()
    {
        var r = PlanAnalysis.Analyze(Plan(Node(cost: 10, children: [Node(cost: 8), Node(cost: 8)]), PlanMode.Estimated));

        Assert.Equal(0, r.Nodes[0].ExclusiveCost);
        Assert.Equal(0, r.Nodes[0].RelativeShare);
    }

    [Fact]
    public void Analyze_SeverityThresholdsAndCounts()
    {
        var plan = Plan(Node(cost: 100, children: [Node(cost: 60), Node(cost: 30), Node(cost: 5)]), PlanMode.Estimated,
            new PlanWarning(PlanMessageCodes.NoStats, []));

        var r = PlanAnalysis.Analyze(plan);

        Assert.Equal(PlanSeverity.Critical, r.Nodes[1].Severity);
        Assert.Equal(PlanSeverity.Alert, r.Nodes[2].Severity);
        Assert.Equal(PlanSeverity.Normal, r.Nodes[3].Severity);
        Assert.Equal(PlanSeverity.Normal, r.Nodes[0].Severity);
        Assert.Equal(1, r.CriticalCount);
        Assert.Equal(1, r.AlertCount);
        Assert.Equal(1, r.WarningCount);
        Assert.Single(r.PlanWarnings);
    }
}
