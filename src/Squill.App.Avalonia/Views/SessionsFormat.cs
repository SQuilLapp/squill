using Squill.Presentation.Activity;

namespace Squill.App.Avalonia.Views;

/// <summary>Maps the monitor view-model's classifications to the design tokens and glyphs the sessions view draws with.</summary>
internal static class SessionsFormat
{
    /// <summary>Gets the token brush of a state dot.</summary>
    /// <param name="kind">The state classification of a row.</param>
    /// <returns>A token brush key.</returns>
    public static string StateBrush(StateDotKind kind) => kind switch
    {
        StateDotKind.Success => "SquillSuccessBrush",
        StateDotKind.Warning => "SquillWarningBrush",
        StateDotKind.Danger => "SquillDangerBrush",
        _ => "SquillFg3Brush",
    };

    /// <summary>Gets the token brush of a duration cell.</summary>
    /// <param name="severity">How long the statement has been running.</param>
    /// <returns>A token brush key.</returns>
    public static string DurationBrush(ActivitySeverity severity) => severity switch
    {
        ActivitySeverity.Danger => "SquillDangerBrush",
        ActivitySeverity.Warning => "SquillWarningBrush",
        _ => "SquillFgBrush",
    };

    /// <summary>Gets the arrow drawn after the header of the sorted column.</summary>
    /// <param name="descending">Whether the sort is descending.</param>
    /// <returns>The arrow glyph.</returns>
    public static string SortArrow(bool descending) => descending ? "↓" : "↑";

    /// <summary>Scales the transactions history to the 0..100 box of the sparkline polyline.</summary>
    /// <param name="values">The samples, oldest first.</param>
    /// <returns>One point per sample (empty below two samples), the highest value at the top.</returns>
    public static IReadOnlyList<(double X, double Y)> Sparkline(IReadOnlyList<double> values)
    {
        if (values.Count < 2)
            return [];
        var min = values.Min();
        var range = Math.Max(values.Max() - min, 1e-9);
        return [.. values.Select((v, i) => (i * 100.0 / (values.Count - 1), 100 - (v - min) / range * 100))];
    }
}
