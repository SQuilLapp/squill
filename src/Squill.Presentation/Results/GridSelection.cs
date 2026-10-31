using System.Text;

namespace Squill.Presentation.Results;

/// <summary>Cell, rectangle or whole-column selection of a grid, independent of any UI.</summary>
public sealed class GridSelection
{
    /// <summary>Gets whether anything is selected.</summary>
    public bool HasSelection { get; private set; }

    /// <summary>Gets whether whole columns (header included) are selected rather than a cell rectangle.</summary>
    public bool WholeColumns { get; private set; }

    /// <summary>Gets the row of the cell that moves with the keyboard and the last click.</summary>
    public long ActiveRow { get; private set; }

    /// <summary>Gets the column of the cell that moves with the keyboard and the last click.</summary>
    public int ActiveColumn { get; private set; }

    private long _anchorRow;
    private int _anchorColumn;

    /// <summary>Gets the first selected column.</summary>
    public int FirstColumn => Math.Min(_anchorColumn, ActiveColumn);

    /// <summary>Gets the last selected column.</summary>
    public int LastColumn => Math.Max(_anchorColumn, ActiveColumn);

    /// <summary>Gets the first selected row.</summary>
    /// <param name="rowCount">The number of loaded rows, used when whole columns are selected.</param>
    /// <returns>The row index.</returns>
    public long FirstRow(long rowCount) => WholeColumns ? 0 : Math.Min(_anchorRow, ActiveRow);

    /// <summary>Gets the last selected row.</summary>
    /// <param name="rowCount">The number of loaded rows, used when whole columns are selected.</param>
    /// <returns>The row index.</returns>
    public long LastRow(long rowCount) => WholeColumns ? rowCount - 1 : Math.Max(_anchorRow, ActiveRow);

    /// <summary>Returns whether a cell lies inside the selection.</summary>
    /// <param name="row">The row index.</param>
    /// <param name="column">The column index.</param>
    /// <param name="rowCount">The number of loaded rows.</param>
    /// <returns><see langword="true"/> when the cell is selected.</returns>
    public bool Contains(long row, int column, long rowCount) =>
        HasSelection && column >= FirstColumn && column <= LastColumn && row >= FirstRow(rowCount) && row <= LastRow(rowCount);

    /// <summary>Returns whether a column header lies inside a whole-column selection.</summary>
    /// <param name="column">The column index.</param>
    /// <returns><see langword="true"/> when the column is selected as a whole.</returns>
    public bool ContainsColumn(int column) => HasSelection && WholeColumns && column >= FirstColumn && column <= LastColumn;

    /// <summary>Clears the selection.</summary>
    public void Clear() => HasSelection = WholeColumns = false;

    /// <summary>Selects a single cell, or extends the rectangle from the anchor.</summary>
    /// <param name="row">The row index.</param>
    /// <param name="column">The column index.</param>
    /// <param name="extend">Keep the anchor (Shift+click).</param>
    public void SelectCell(long row, int column, bool extend)
    {
        if (!extend || !HasSelection)
        {
            _anchorRow = row;
            _anchorColumn = column;
            WholeColumns = false;
        }

        ActiveRow = row;
        ActiveColumn = column;
        HasSelection = true;
    }

    /// <summary>Selects a whole column, or extends the column range from the anchor.</summary>
    /// <param name="column">The column index.</param>
    /// <param name="extend">Keep the anchor column (Shift+click).</param>
    public void SelectColumn(int column, bool extend)
    {
        if (!extend || !HasSelection)
            _anchorColumn = column;
        _anchorRow = 0;
        ActiveRow = 0;
        ActiveColumn = column;
        WholeColumns = true;
        HasSelection = true;
    }

    /// <summary>Selects every loaded cell.</summary>
    /// <param name="rowCount">The number of loaded rows.</param>
    /// <param name="columnCount">The number of columns.</param>
    public void SelectAll(long rowCount, int columnCount)
    {
        if (rowCount == 0 || columnCount == 0)
        {
            Clear();
            return;
        }

        _anchorRow = 0;
        _anchorColumn = 0;
        ActiveRow = rowCount - 1;
        ActiveColumn = columnCount - 1;
        WholeColumns = true;
        HasSelection = true;
    }

    /// <summary>Moves the active cell, clamped to the grid.</summary>
    /// <param name="rowDelta">Rows to move (negative is up).</param>
    /// <param name="columnDelta">Columns to move (negative is left).</param>
    /// <param name="rowCount">The number of loaded rows.</param>
    /// <param name="columnCount">The number of columns.</param>
    /// <param name="extend">Keep the anchor (Shift+arrow).</param>
    public void Move(long rowDelta, long columnDelta, long rowCount, int columnCount, bool extend)
    {
        if (rowCount == 0 || columnCount == 0)
            return;
        if (!HasSelection)
        {
            SelectCell(0, 0, false);
            return;
        }

        var row = Math.Clamp(ActiveRow + rowDelta, 0, rowCount - 1);
        var column = (int)Math.Clamp(ActiveColumn + columnDelta, 0, columnCount - 1);
        if (WholeColumns && extend)
            ActiveColumn = column;
        else
            SelectCell(row, column, extend);
    }

    /// <summary>Formats the selection as tab-separated text, nulls as empty cells.</summary>
    /// <param name="source">The data the selection refers to.</param>
    /// <returns>The text, with a header row when whole columns are selected; empty when nothing is selected.</returns>
    public string ToTsv(IGridDataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!HasSelection || source.RowCount == 0)
            return "";
        var last = Math.Min(LastColumn, source.Columns.Count - 1);
        var sb = new StringBuilder();
        if (WholeColumns)
        {
            for (var c = FirstColumn; c <= last; c++)
                sb.Append(c > FirstColumn ? "\t" : "").Append(source.Columns[c].Name);
            sb.Append('\n');
        }

        var lastRow = Math.Min(LastRow(source.RowCount), source.RowCount - 1);
        for (var r = FirstRow(source.RowCount); r <= lastRow; r++)
        {
            for (var c = FirstColumn; c <= last; c++)
                sb.Append(c > FirstColumn ? "\t" : "").Append(CellFormatter.Format(source.GetCell(r, c), source.Columns[c]));
            sb.Append('\n');
        }

        return sb.ToString();
    }
}
