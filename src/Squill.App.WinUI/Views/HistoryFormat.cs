using System.Globalization;
using Squill.Platform.Storage;
using Squill.Presentation.Localization;
using Squill.Presentation.Results;

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
    public static string Detail(HistoryEntry entry, DateTimeOffset now) => DisplayFormat.HistoryDetail(entry, now);
}
