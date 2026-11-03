using System.Globalization;
using Squill.Platform.Storage;
using Squill.Presentation.Localization;

namespace Squill.App.WinUI.Views;

/// <summary>Builds the texts of a history entry row.</summary>
internal static class HistoryFormat
{
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
        var time = at.Date == now.ToLocalTime().Date
            ? at.ToString("HH:mm", CultureInfo.InvariantCulture)
            : at.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
        var elapsed = entry.Elapsed.TotalSeconds < 1
            ? string.Create(CultureInfo.InvariantCulture, $"{entry.Elapsed.TotalMilliseconds:0} ms")
            : string.Create(CultureInfo.InvariantCulture, $"{entry.Elapsed.TotalSeconds:0.0} s");
        var parts = new List<string> { time, elapsed };
        if (entry.RowCount is { } rows)
            parts.Add($"{rows.ToString("N0", CultureInfo.GetCultureInfo("fr-FR"))} {UiText.Get(rows == 1 ? "editor.row" : "editor.rows")}");
        return string.Join(" · ", parts);
    }
}
