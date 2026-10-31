using System.Globalization;
using Squill.Platform.Storage;

namespace Squill.App.WinUI.Views;

/// <summary>Functions used by the compiled bindings of the history template.</summary>
public static class HistoryFormat
{
    /// <summary>Builds the second line of an entry: outcome, start time and duration.</summary>
    /// <param name="entry">The history entry.</param>
    /// <returns>The text.</returns>
    public static string Detail(HistoryEntry entry) =>
        string.Create(CultureInfo.CurrentCulture, $"{(entry.Succeeded ? "OK" : "Failed")}  {entry.At:g}  {entry.Elapsed:m\\:ss\\.fff}");
}
