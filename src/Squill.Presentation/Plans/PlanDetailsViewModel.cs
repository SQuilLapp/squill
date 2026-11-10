using Squill.Core.Plans;
using Squill.Presentation.Localization;

namespace Squill.Presentation.Plans;

/// <summary>The details panel of the selected plan node.</summary>
public sealed class PlanDetailsViewModel
{
    internal PlanDetailsViewModel(PlanRowViewModel row, QueryPlan plan)
    {
        var node = row.Node;
        Title = node.Target is null ? node.Operation : UiText.Format("plan.details.title", node.Operation, node.Target);
        ShareText = row.RelativeShare is null ? "" : UiText.Format(plan.Root.ActualTimeMs is null ? "plan.details.shareCost" : "plan.details.shareTime", row.RelativeText);
        var properties = new List<PlanProperty>();
        if (node.Detail is { } detail)
            properties.Add(new PlanProperty(UiText.Get("plan.prop.condition"), detail));
        properties.AddRange(node.Properties.Where(p => PlanText.IsKnownProperty(p.Name)).Select(p => new PlanProperty(PlanText.PropertyName(p.Name), p.Value)));
        properties.AddRange(node.Properties.Where(p => !PlanText.IsKnownProperty(p.Name)));
        Properties = properties;
        Warnings = [.. row.Stats.Warnings.Select(PlanText.Warning)];
        Suggestions = plan.Suggestions;
        CopyText = string.Join(Environment.NewLine, plan.Suggestions.Select(s => s.Text));
    }

    /// <summary>Gets the title: the operation, and <c>sur</c> and the target when the node reads one.</summary>
    public string Title { get; }

    /// <summary>Gets the sentence giving the exclusive share of the node, empty when the engine gave no cost or time.</summary>
    public string ShareText { get; }

    /// <summary>Gets the properties in display order: the condition, then the translated known ones, then the others as the engine prints them.</summary>
    public IReadOnlyList<PlanProperty> Properties { get; }

    /// <summary>Gets the texts of the warnings of this node.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>Gets the index suggestions the engine reported for the whole statement (SQL Server only).</summary>
    public IReadOnlyList<PlanSuggestion> Suggestions { get; }

    /// <summary>Gets a value indicating whether the suggestion card is shown.</summary>
    public bool HasSuggestions => Suggestions.Count > 0;

    /// <summary>Gets the text a head puts on the clipboard for the suggestion card: every suggested statement, one per line.</summary>
    public string CopyText { get; }
}
