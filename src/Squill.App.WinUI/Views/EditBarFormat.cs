using System.Globalization;
using Squill.Core.Editing;
using Squill.Presentation.Localization;

namespace Squill.App.WinUI.Views;

/// <summary>Text and lookups of the edit bar that do not need a UI thread.</summary>
internal static class EditBarFormat
{
    /// <summary>Builds the line shown for an issue in the issues flyout.</summary>
    /// <param name="issue">The issue.</param>
    /// <returns><c>Row n, column: message</c>, without the column part for a row-level issue.</returns>
    public static string Describe(EditIssue issue) =>
        $"Row {issue.Row + 1}{(issue.Column is null ? "" : ", " + issue.Column)}: {issue.Message}";

    /// <summary>Finds the grid column an issue points at.</summary>
    /// <param name="issue">The issue.</param>
    /// <param name="columnNames">The column names in grid order.</param>
    /// <returns>The column index, or 0 for a row-level or unknown column.</returns>
    public static int ColumnIndex(EditIssue issue, IReadOnlyList<string> columnNames) =>
        issue.Column is null ? 0 : Math.Max(0, columnNames.ToList().IndexOf(issue.Column));

    /// <summary>Builds the bold text of the pending-edits banner.</summary>
    /// <param name="count">The number of pending row changes.</param>
    /// <returns>The localized <c>n modification(s) en attente</c> text.</returns>
    public static string Pending(int count) =>
        UiText.Format(count == 1 ? "results.pendingChanges.one" : "results.pendingChanges.other", count);

    /// <summary>Builds the muted detail of the pending-edits banner.</summary>
    /// <param name="updates">The number of updated rows.</param>
    /// <param name="inserts">The number of inserted rows.</param>
    /// <param name="deletes">The number of deleted rows.</param>
    /// <returns>The non-zero counts joined with <c> · </c>, in the order updates, insertions, deletions.</returns>
    public static string Changes(int updates, int inserts, int deletes)
    {
        var parts = new List<string>(3);
        Add(parts, updates, "results.updates");
        Add(parts, inserts, "results.inserts");
        Add(parts, deletes, "results.deletes");
        return string.Join(" · ", parts);

        static void Add(List<string> parts, int n, string key)
        {
            if (n > 0)
                parts.Add(UiText.Format(n == 1 ? key + ".one" : key + ".other", n));
        }
    }

    /// <summary>Builds the read/write marker next to the result tabs.</summary>
    /// <param name="keyColumns">The key columns of an editable result, or <see langword="null"/> when the result is read-only.</param>
    /// <param name="readOnlyReason">The reason a result is read-only; <see langword="null"/> or <c>loading</c> shows no marker.</param>
    /// <returns>The marker text and whether it marks an editable result, or <see langword="null"/> for no marker.</returns>
    public static (string Text, bool Editable)? Marker(IReadOnlyList<string>? keyColumns, string? readOnlyReason)
    {
        if (keyColumns is not null)
            return (UiText.Format("results.editable", string.Join(", ", keyColumns)), true);
        return readOnlyReason is null or "loading" ? null : (UiText.Format("results.readOnly", readOnlyReason), false);
    }

    /// <summary>Formats a loaded row count with grouped digits, as in the mockups.</summary>
    /// <param name="rows">The row count.</param>
    /// <returns>The count in the French culture.</returns>
    public static string Rows(long rows) => rows.ToString("N0", CultureInfo.GetCultureInfo("fr-FR"));

    /// <summary>Builds the footer of the results panel.</summary>
    /// <param name="rows">The loaded row count.</param>
    /// <param name="truncated">Whether the row limit cut the result.</param>
    /// <returns>The localized <c>Lignes 1-n sur m chargées</c> text.</returns>
    public static string Footer(long rows, bool truncated)
    {
        var loaded = Rows(rows);
        return UiText.Format("results.footer", loaded, truncated ? loaded + "+" : loaded);
    }
}
