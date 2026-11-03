using System.Globalization;
using Avalonia.Data.Converters;
using Squill.Platform.Storage;
using Squill.Presentation.Localization;
using Squill.Presentation.Results;

namespace Squill.App.Avalonia.Views;

/// <summary>Builds the texts of a history entry row; used by the compiled bindings of the history template.</summary>
public static class HistoryFormat
{
    /// <summary>Gets the converter that turns an entry into its detail line, for XAML bindings.</summary>
    public static readonly IValueConverter DetailConverter = new FuncValueConverter<HistoryEntry?, string?>(e => e is null ? null : Detail(e));

    /// <summary>Builds the detail line of an entry: start time, duration and row count, separated by a middle dot.</summary>
    /// <param name="entry">The history entry.</param>
    /// <returns>The text; the row count is left out when it is unknown.</returns>
    public static string Detail(HistoryEntry entry) => Detail(entry, DateTimeOffset.Now);

    /// <summary>Builds the detail line of an entry relative to a given moment.</summary>
    /// <param name="entry">The history entry.</param>
    /// <param name="now">The current moment; an entry of the same day shows only the time.</param>
    /// <returns>The text; the row count is left out when it is unknown.</returns>
    public static string Detail(HistoryEntry entry, DateTimeOffset now)
    {
        var at = entry.At.ToLocalTime();
        var time = DisplayFormat.RecentTime(at, now.ToLocalTime());
        var elapsed = DisplayFormat.Duration(entry.Elapsed);
        var parts = new List<string> { time, elapsed };
        if (entry.RowCount is { } rows)
            parts.Add($"{DisplayFormat.Count(rows)} {UiText.Get(rows == 1 ? "editor.row" : "editor.rows")}");
        return string.Join(" · ", parts);
    }
}
