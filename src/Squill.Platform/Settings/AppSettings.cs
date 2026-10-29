using Squill.Core;

namespace Squill.Platform.Settings;

/// <summary>User preferences consumed by the view-models.</summary>
/// <param name="Theme">Requested color theme.</param>
/// <param name="EditorFontSize">Editor font size in points, between 8 and 32.</param>
/// <param name="MaxGridRows">Maximum number of rows kept in a result grid, between 1,000 and 10,000,000.</param>
/// <param name="FormatterKeywordCase">Keyword casing applied by the SQL formatter.</param>
public sealed record AppSettings(ThemeMode Theme, double EditorFontSize, int MaxGridRows, KeywordCase FormatterKeywordCase)
{
    /// <summary>Gets the settings used when nothing has been saved.</summary>
    public static AppSettings Default { get; } = new(ThemeMode.System, 13, 1_000_000, KeywordCase.Upper);

    /// <summary>Returns a copy whose numeric values are forced into their allowed range.</summary>
    /// <returns>The clamped settings.</returns>
    public AppSettings Clamp() => this with
    {
        EditorFontSize = Math.Clamp(EditorFontSize, 8, 32),
        MaxGridRows = Math.Clamp(MaxGridRows, 1_000, 10_000_000),
    };
}
