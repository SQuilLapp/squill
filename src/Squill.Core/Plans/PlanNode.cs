namespace Squill.Core.Plans;

/// <summary>One operator of a query plan, translated from an engine-specific format.</summary>
/// <param name="Id">Pre-order index, root = 0, unique per plan.</param>
/// <param name="Operation">Operator name as displayed.</param>
/// <param name="Kind">Operator category (icon).</param>
/// <param name="Target">Table or index the operator reads, if any.</param>
/// <param name="Detail">Extra one-line detail (condition, keys), if any.</param>
/// <param name="EstimatedRows">Estimated rows per execution; <see langword="null"/> when not reported.</param>
/// <param name="EstimatedCost">Cumulative estimated cost (this node and everything below it).</param>
/// <param name="StartupCost">Estimated cost before the first row.</param>
/// <param name="RowWidth">Estimated average row width in bytes.</param>
/// <param name="ActualRows">Actual rows, total over all loops.</param>
/// <param name="ActualTimeMs">Actual time in milliseconds, inclusive and total over all loops.</param>
/// <param name="Loops">Number of executions of the node.</param>
/// <param name="Properties">Further properties for the details panel.</param>
/// <param name="Warnings">Warnings reported by the engine for this node.</param>
/// <param name="IsHelper">True when the graph draws the node as an edge pill instead of a card.</param>
/// <param name="Children">Input operators.</param>
public sealed record PlanNode(
    int Id,
    string Operation,
    PlanNodeKind Kind,
    string? Target,
    string? Detail,
    double? EstimatedRows,
    double? EstimatedCost,
    double? StartupCost,
    int? RowWidth,
    double? ActualRows,
    double? ActualTimeMs,
    long? Loops,
    IReadOnlyList<PlanProperty> Properties,
    IReadOnlyList<PlanWarning> Warnings,
    bool IsHelper,
    IReadOnlyList<PlanNode> Children);
