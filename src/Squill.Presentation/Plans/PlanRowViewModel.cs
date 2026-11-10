using Squill.Core.Plans;

namespace Squill.Presentation.Plans;

/// <summary>One line of the plan table: a node, its indentation and its cells already formatted for the current language.</summary>
public sealed class PlanRowViewModel
{
    internal PlanRowViewModel(PlanNode node, int depth, PlanNodeStats stats)
    {
        Node = node;
        Depth = depth;
        Stats = stats;
        RelativeText = PlanText.Percent(stats.RelativeShare);
        TotalCost = PlanText.Cost(node.EstimatedCost);
        EstimatedRows = PlanText.Count(node.EstimatedRows);
        ActualRows = PlanText.Count(node.ActualRows);
        Time = PlanText.Milliseconds(node.ActualTimeMs);
        Loops = PlanText.Count(node.Loops);
        MisestimateText = PlanText.Factor(stats.MisestimateFactor);
    }

    /// <summary>Gets the plan node of this row.</summary>
    public PlanNode Node { get; }

    /// <summary>Gets the number of ancestors, which the table turns into indentation.</summary>
    public int Depth { get; }

    /// <summary>Gets the analysis figures of the node (shares, severity, warnings).</summary>
    public PlanNodeStats Stats { get; }

    /// <summary>Gets the operation name.</summary>
    public string Label => Node.Operation;

    /// <summary>Gets the table or index read, shown in monospace, or null.</summary>
    public string? Target => Node.Target;

    /// <summary>Gets the exclusive share as a whole percentage (<c>64 %</c>), or an en dash.</summary>
    public string RelativeText { get; }

    /// <summary>Gets the exclusive share in [0, 1] for the bar, or null when the engine gave no cost or time.</summary>
    public double? RelativeShare => Stats.RelativeShare;

    /// <summary>Gets the cumulative estimated cost.</summary>
    public string TotalCost { get; }

    /// <summary>Gets the estimated rows.</summary>
    public string EstimatedRows { get; }

    /// <summary>Gets the actual rows, an en dash for an estimated plan.</summary>
    public string ActualRows { get; }

    /// <summary>Gets the actual time, inclusive of the children.</summary>
    public string Time { get; }

    /// <summary>Gets the number of executions of the node.</summary>
    public string Loops { get; }

    /// <summary>Gets the severity driving the border color of the node.</summary>
    public PlanSeverity Severity => Stats.Severity;

    /// <summary>Gets the estimate error as a multiplier (<c>x590</c>), empty when the estimate was close.</summary>
    public string MisestimateText { get; }

    /// <summary>Gets a value indicating whether the graph draws this node as an edge pill instead of a card.</summary>
    public bool IsHelper => Node.IsHelper;
}
