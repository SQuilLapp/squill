namespace Squill.Presentation.Plans;

/// <summary>One labelled summary figure of the plan toolbar, already formatted for the current language.</summary>
/// <param name="Label">What the figure is (<c>Durée totale</c>).</param>
/// <param name="Value">The formatted value (<c>1 182 ms</c>).</param>
public sealed record PlanFigure(string Label, string Value);
