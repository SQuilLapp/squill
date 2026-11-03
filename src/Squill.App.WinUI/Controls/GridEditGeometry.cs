using Squill.Presentation.Results;

namespace Squill.App.WinUI.Controls;

/// <summary>A rectangle in grid-surface coordinates, free of UI types so it can be unit-tested.</summary>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Width.</param>
/// <param name="Height">Height.</param>
public readonly record struct CellBox(double X, double Y, double Width, double Height);

/// <summary>Which soft background a row or cell of an editable grid is painted with.</summary>
public enum GridTint
{
    /// <summary>No tint.</summary>
    None,

    /// <summary>Pending changes (accent).</summary>
    Modified,

    /// <summary>Row added and not yet committed (success).</summary>
    Inserted,

    /// <summary>Row marked for deletion (danger).</summary>
    Deleted,
}

/// <summary>Pure geometry and style decisions of the editable grid, kept apart from Win2D and XAML.</summary>
internal static class GridEditGeometry
{
    /// <summary>Gets where the in-place editor goes for a cell.</summary>
    /// <param name="layout">Column and row geometry.</param>
    /// <param name="row">Grid row index.</param>
    /// <param name="column">Column index.</param>
    /// <param name="scrollX">Horizontal scroll offset.</param>
    /// <param name="scrollY">Vertical scroll offset.</param>
    /// <param name="viewportWidth">Width of the drawing surface.</param>
    /// <param name="bodyHeight">Height of the body below the header.</param>
    /// <param name="gutter">Width of the row-number column left of the data columns.</param>
    /// <returns>The cell rectangle in surface coordinates, or <see langword="null"/> when its top-left corner or bottom-left corner is scrolled out of the body.</returns>
    public static CellBox? EditorBox(GridLayout layout, long row, int column, double scrollX, double scrollY, double viewportWidth, double bodyHeight, double gutter = 0)
    {
        if (column < 0 || column >= layout.ColumnCount)
        {
            return null;
        }

        var box = new CellBox(
            gutter + layout.ColumnLeft(column) - scrollX,
            layout.HeaderHeight + (row * layout.RowHeight) - scrollY,
            layout.ColumnWidth(column),
            layout.RowHeight);
        var bottom = box.Y + box.Height - 0.5;
        var visible = box.X >= gutter && box.X < viewportWidth
            && box.Y >= layout.HeaderHeight && box.Y < layout.HeaderHeight + bodyHeight
            && bottom >= layout.HeaderHeight && bottom < layout.HeaderHeight + bodyHeight;
        return visible ? box : null;
    }

    /// <summary>Gets the background of a whole row.</summary>
    /// <param name="state">The row state.</param>
    /// <returns>The tint to fill the row with.</returns>
    public static GridTint RowTint(RowState state) => state switch
    {
        RowState.Inserted => GridTint.Inserted,
        RowState.Deleted => GridTint.Deleted,
        RowState.Modified => GridTint.Modified,
        _ => GridTint.None,
    };

    /// <summary>Gets whether a cell gets the extra modified tint on top of its row tint.</summary>
    /// <param name="state">The row state.</param>
    /// <param name="cellModified">Whether the cell value differs from the database.</param>
    /// <returns><see langword="true"/> for a modified cell of a pre-existing row.</returns>
    public static bool ModifiedCellTint(RowState state, bool cellModified) => cellModified && state != RowState.Inserted;

    /// <summary>Gets the text of the row-number column for a row.</summary>
    /// <param name="state">The row state.</param>
    /// <param name="row">Zero-based grid row index.</param>
    /// <returns><c>+</c> for an added row, <c>-</c> for a deleted one, else the one-based row number.</returns>
    public static string GutterLabel(RowState state, long row) => state switch
    {
        RowState.Inserted => "+",
        RowState.Deleted => "-",
        _ => (row + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    /// <summary>Gets the colored bar at the left edge of an added or deleted row.</summary>
    /// <param name="state">The row state.</param>
    /// <param name="top">Top edge of the row.</param>
    /// <param name="rowHeight">Height of the row.</param>
    /// <param name="width">Bar width.</param>
    /// <returns>The bar rectangle, or <see langword="null"/> for rows that have none.</returns>
    public static CellBox? StateBar(RowState state, double top, double rowHeight, double width) =>
        state is RowState.Inserted or RowState.Deleted ? new CellBox(0, top, width, rowHeight) : null;

    /// <summary>Gets where the column type text starts in a header.</summary>
    /// <param name="nameLeft">Left edge of the column name.</param>
    /// <param name="nameWidth">Width of the column name.</param>
    /// <returns>The left edge of the type text, 6 px after the name.</returns>
    public static double HeaderTypeX(double nameLeft, double nameWidth) => nameLeft + nameWidth + 6;
}
