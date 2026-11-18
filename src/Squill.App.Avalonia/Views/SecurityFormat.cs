using Squill.Presentation.Security;

namespace Squill.App.Avalonia.Views;

/// <summary>Maps the privilege matrix classifications to the design tokens, glyphs, catalog keys and keyboard moves the security view draws with.</summary>
internal static class SecurityFormat
{
    /// <summary>Gets the key of the localized state name of a cell, used for the tooltip legend and the Narrator text.</summary>
    /// <param name="display">How the cell is drawn.</param>
    /// <returns>A catalog key.</returns>
    public static string StateKey(PrivilegeCellDisplay display) => display switch
    {
        PrivilegeCellDisplay.Granted => "security.legend.granted",
        PrivilegeCellDisplay.Inherited => "security.legend.inherited",
        PrivilegeCellDisplay.PendingGrant => "security.legend.pendingGrant",
        PrivilegeCellDisplay.PendingRevoke => "security.legend.pendingRevoke",
        _ => "security.legend.none",
    };

    /// <summary>Gets the token brush that fills the box of a cell; <see langword="null"/> when the box is only outlined.</summary>
    /// <param name="display">How the cell is drawn.</param>
    /// <returns>A token brush key, or <see langword="null"/>.</returns>
    public static string? FillBrush(PrivilegeCellDisplay display) => display switch
    {
        PrivilegeCellDisplay.Granted => "SquillAccentBrush",
        PrivilegeCellDisplay.Inherited => "SquillFg3Brush",
        _ => null,
    };

    /// <summary>Gets the token brush of the outline of a cell box.</summary>
    /// <param name="display">How the cell is drawn.</param>
    /// <returns>A token brush key.</returns>
    public static string OutlineBrush(PrivilegeCellDisplay display) => display switch
    {
        PrivilegeCellDisplay.Granted => "SquillAccentBrush",
        PrivilegeCellDisplay.Inherited => "SquillFg3Brush",
        PrivilegeCellDisplay.PendingGrant => "SquillAccentBrush",
        PrivilegeCellDisplay.PendingRevoke => "SquillDangerBrush",
        _ => "SquillBorderBrush",
    };

    /// <summary>Gets the glyph drawn inside the box: a check when held, a minus for a pending revoke, nothing otherwise.</summary>
    /// <param name="display">How the cell is drawn.</param>
    /// <returns>The glyph, or an empty text.</returns>
    public static string Glyph(PrivilegeCellDisplay display) => display switch
    {
        PrivilegeCellDisplay.Granted or PrivilegeCellDisplay.Inherited => "✓",
        PrivilegeCellDisplay.PendingRevoke => "−",
        _ => "",
    };

    /// <summary>Gets the cell the focus moves to after an arrow key, clamped to the matrix.</summary>
    /// <param name="rowStep">The row offset of the key: -1 up, 1 down, 0 otherwise.</param>
    /// <param name="columnStep">The column offset of the key: -1 left, 1 right, 0 otherwise.</param>
    /// <param name="row">The current row.</param>
    /// <param name="column">The current column.</param>
    /// <param name="rows">The number of rows.</param>
    /// <param name="columns">The number of columns.</param>
    /// <returns>The new position.</returns>
    public static (int Row, int Column) Move(int rowStep, int columnStep, int row, int column, int rows, int columns) =>
        (Math.Clamp(row + rowStep, 0, Math.Max(rows - 1, 0)), Math.Clamp(column + columnStep, 0, Math.Max(columns - 1, 0)));

    /// <summary>Gets the cell under a point of the matrix body.</summary>
    /// <param name="x">Horizontal offset from the left edge of the matrix.</param>
    /// <param name="y">Vertical offset from the top of the first row.</param>
    /// <param name="nameWidth">Width of the object name column.</param>
    /// <param name="cellWidth">Width of a privilege column.</param>
    /// <param name="rowHeight">Height of a row.</param>
    /// <param name="rows">The number of rows.</param>
    /// <param name="columns">The number of privilege columns.</param>
    /// <returns>The row and column, or <see langword="null"/> over the name column or outside the matrix.</returns>
    public static (int Row, int Column)? HitTest(double x, double y, double nameWidth, double cellWidth, double rowHeight, int rows, int columns)
    {
        if (x < nameWidth || y < 0)
            return null;
        var row = (int)(y / rowHeight);
        var column = (int)((x - nameWidth) / cellWidth);
        return row < rows && column < columns ? (row, column) : null;
    }
}
