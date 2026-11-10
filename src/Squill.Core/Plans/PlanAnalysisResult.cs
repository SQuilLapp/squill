namespace Squill.Core.Plans;

/// <summary>Figures computed for one plan node by <see cref="PlanAnalysis"/>.</summary>
/// <param name="ExclusiveCost">Cost of the node alone (inclusive minus children), or <see langword="null"/> when no cost is known.</param>
/// <param name="ExclusiveTimeMs">Time of the node alone in milliseconds, or <see langword="null"/> when no timing is known.</param>
/// <param name="RelativeShare">Exclusive share of the plan, in [0, 1]; <see langword="null"/> when nothing can be compared.</param>
/// <param name="Severity">Flag level of the node.</param>
/// <param name="MisestimateFactor">Ratio between estimated and actual rows when it is 10 or more; otherwise <see langword="null"/>.</param>
/// <param name="Warnings">Provider warnings of the node plus the generic ones added by the analysis.</param>
public sealed record PlanNodeStats(
    double? ExclusiveCost,
    double? ExclusiveTimeMs,
    double? RelativeShare,
    PlanSeverity Severity,
    double? MisestimateFactor,
    IReadOnlyList<PlanWarning> Warnings);

/// <summary>Outcome of <see cref="PlanAnalysis.Analyze"/>.</summary>
/// <param name="Nodes">Statistics keyed by <see cref="PlanNode.Id"/>.</param>
/// <param name="PlanWarnings">Plan-level warnings, carried through from the plan.</param>
/// <param name="CriticalCount">Number of nodes of severity <see cref="PlanSeverity.Critical"/>.</param>
/// <param name="AlertCount">Number of nodes of severity <see cref="PlanSeverity.Alert"/>.</param>
/// <param name="WarningCount">Number of distinct warnings over all nodes and the plan.</param>
public sealed record PlanAnalysisResult(
    IReadOnlyDictionary<int, PlanNodeStats> Nodes,
    IReadOnlyList<PlanWarning> PlanWarnings,
    int CriticalCount,
    int AlertCount,
    int WarningCount);
