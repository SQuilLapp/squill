using Squill.Presentation.Activity;

namespace Squill.App.WinUI.Views;

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
}
