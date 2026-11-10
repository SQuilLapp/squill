using System.Globalization;

namespace Squill.Core.Plans;

/// <summary>Turns a <see cref="QueryPlan"/> into exclusive figures, severities and generic warnings, identically for every engine.</summary>
public static class PlanAnalysis
{
    /// <summary>Exclusive share from which a node is an alert.</summary>
    public const double AlertShare = 0.30;

    /// <summary>Exclusive share from which a node is critical.</summary>
    public const double CriticalShare = 0.60;

    /// <summary>Ratio between estimated and actual rows from which <see cref="PlanMessageCodes.Misestimate"/> is raised.</summary>
    public const double MisestimateRatio = 10;

    /// <summary>Estimated row count from which a scan without limit is reported as <see cref="PlanMessageCodes.FullScan"/>.</summary>
    public const double FullScanRows = 10_000;

    /// <summary>Analyzes a plan.</summary>
    /// <param name="plan">The plan to analyze.</param>
    /// <returns>Per-node statistics and counters.</returns>
    public static PlanAnalysisResult Analyze(QueryPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var useTime = plan.Root.ActualTimeMs is not null;
        var rootTotal = useTime ? plan.Root.ActualTimeMs : plan.Root.EstimatedCost;
        var nodes = new Dictionary<int, PlanNodeStats>();
        var distinct = new HashSet<string>(StringComparer.Ordinal);
        var critical = 0;
        var alert = 0;
        Visit(plan.Root, plan.Mode == PlanMode.Actual, underLimit: false);

        foreach (var w in plan.Warnings)
        {
            distinct.Add(Key(w));
        }

        return new PlanAnalysisResult(nodes, plan.Warnings, critical, alert, distinct.Count);

        void Visit(PlanNode node, bool actual, bool underLimit)
        {
            var exclusiveCost = Exclusive(node, n => n.EstimatedCost);
            var exclusiveTime = Exclusive(node, n => n.ActualTimeMs);
            var exclusive = useTime ? exclusiveTime : exclusiveCost;
            double? share = exclusive is { } e && rootTotal is > 0 ? Math.Clamp(e / rootTotal.Value, 0, 1) : null;

            var warnings = new List<PlanWarning>(node.Warnings);
            double? factor = null;
            if (actual && node.EstimatedRows is { } est && node.ActualRows is { } act)
            {
                var perLoop = act / Math.Max(node.Loops ?? 1, 1);
                var a = perLoop == 0 ? 1 : perLoop;
                var s = est == 0 ? 1 : est;
                var ratio = Math.Max(a / s, s / a);
                if (ratio >= MisestimateRatio)
                {
                    factor = ratio;
                    warnings.Add(new PlanWarning(PlanMessageCodes.Misestimate, [ratio.ToString("0.#", CultureInfo.InvariantCulture), a > s ? "under" : "over"]));
                }
            }

            if (node.Kind == PlanNodeKind.TableScan && !underLimit && node.EstimatedRows is >= FullScanRows)
            {
                warnings.Add(new PlanWarning(PlanMessageCodes.FullScan, []));
            }

            var severity = share >= CriticalShare ? PlanSeverity.Critical
                : share >= AlertShare || warnings.Count > 0 ? PlanSeverity.Alert
                : PlanSeverity.Normal;
            if (severity == PlanSeverity.Critical)
            {
                critical++;
            }
            else if (severity == PlanSeverity.Alert)
            {
                alert++;
            }

            foreach (var w in warnings)
            {
                distinct.Add(Key(w));
            }

            nodes[node.Id] = new PlanNodeStats(exclusiveCost, exclusiveTime, share, severity, factor, warnings);
            var childUnderLimit = underLimit || node.Kind == PlanNodeKind.Limit;
            foreach (var child in node.Children)
            {
                Visit(child, actual, childUnderLimit);
            }
        }
    }

    private static double? Exclusive(PlanNode node, Func<PlanNode, double?> metric)
    {
        if (metric(node) is not { } own)
        {
            return null;
        }

        var below = 0.0;
        foreach (var child in node.Children)
        {
            below += metric(child) ?? 0;
        }

        return Math.Max(own - below, 0);
    }

    private static string Key(PlanWarning w) => w.Code + "\u001f" + string.Join('\u001f', w.Args);
}
