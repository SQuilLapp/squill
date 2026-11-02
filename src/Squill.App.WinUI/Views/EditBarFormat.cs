using Squill.Core.Editing;

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
}
