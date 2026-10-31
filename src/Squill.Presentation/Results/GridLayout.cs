namespace Squill.Presentation.Results;

/// <summary>Pure geometry of the virtualized grid: column offsets, visible ranges and hit-testing, with no UI types.</summary>
public sealed class GridLayout
{
    /// <summary>Smallest column width in device-independent pixels.</summary>
    public const double MinColumnWidth = 60;

    /// <summary>Largest column width in device-independent pixels.</summary>
    public const double MaxColumnWidth = 400;

    private double[] _lefts = [0];

    /// <summary>Creates a layout.</summary>
    /// <param name="rowHeight">Height of one body row.</param>
    /// <param name="headerHeight">Height of the fixed header row.</param>
    public GridLayout(double rowHeight, double headerHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rowHeight);
        ArgumentOutOfRangeException.ThrowIfNegative(headerHeight);
        RowHeight = rowHeight;
        HeaderHeight = headerHeight;
    }

    /// <summary>Gets the height of one body row.</summary>
    public double RowHeight { get; }

    /// <summary>Gets the height of the fixed header row.</summary>
    public double HeaderHeight { get; }

    /// <summary>Gets the number of columns.</summary>
    public int ColumnCount => _lefts.Length - 1;

    /// <summary>Gets the sum of all column widths.</summary>
    public double TotalWidth => _lefts[^1];

    /// <summary>Clamps a width to the allowed range.</summary>
    /// <param name="width">The requested width.</param>
    /// <returns>The width within <see cref="MinColumnWidth"/> and <see cref="MaxColumnWidth"/>.</returns>
    public static double ClampWidth(double width) => Math.Clamp(width, MinColumnWidth, MaxColumnWidth);

    /// <summary>Replaces all column widths (each clamped).</summary>
    /// <param name="widths">The new widths, one per column.</param>
    public void SetColumnWidths(IReadOnlyList<double> widths)
    {
        ArgumentNullException.ThrowIfNull(widths);
        var lefts = new double[widths.Count + 1];
        for (var i = 0; i < widths.Count; i++)
        {
            lefts[i + 1] = lefts[i] + ClampWidth(widths[i]);
        }

        _lefts = lefts;
    }

    /// <summary>Gets the width of a column.</summary>
    /// <param name="column">Zero-based column index.</param>
    /// <returns>The width.</returns>
    public double ColumnWidth(int column) => _lefts[column + 1] - _lefts[column];

    /// <summary>Gets the left edge of a column in content coordinates.</summary>
    /// <param name="column">Zero-based column index, or <see cref="ColumnCount"/> for the right edge.</param>
    /// <returns>The left edge.</returns>
    public double ColumnLeft(int column) => _lefts[column];

    /// <summary>Resizes one column (clamped) and shifts the following ones.</summary>
    /// <param name="column">Zero-based column index.</param>
    /// <param name="width">The requested width.</param>
    public void SetColumnWidth(int column, double width)
    {
        var delta = ClampWidth(width) - ColumnWidth(column);
        for (var i = column + 1; i < _lefts.Length; i++)
        {
            _lefts[i] += delta;
        }
    }

    /// <summary>Gets the height of all body rows.</summary>
    /// <param name="rowCount">The number of rows.</param>
    /// <returns>The content height without the header.</returns>
    public double TotalHeight(long rowCount) => rowCount * RowHeight;

    /// <summary>Gets the largest useful vertical scroll offset.</summary>
    /// <param name="rowCount">The number of rows.</param>
    /// <param name="bodyHeight">The visible height below the header.</param>
    /// <returns>The maximum offset, never negative.</returns>
    public double MaxScrollY(long rowCount, double bodyHeight) => Math.Max(0, TotalHeight(rowCount) - bodyHeight);

    /// <summary>Gets the largest useful horizontal scroll offset.</summary>
    /// <param name="viewportWidth">The visible width.</param>
    /// <returns>The maximum offset, never negative.</returns>
    public double MaxScrollX(double viewportWidth) => Math.Max(0, TotalWidth - viewportWidth);

    /// <summary>Gets the rows that intersect the body viewport.</summary>
    /// <param name="scrollY">The vertical scroll offset.</param>
    /// <param name="bodyHeight">The visible height below the header.</param>
    /// <param name="rowCount">The number of rows.</param>
    /// <returns>The first visible row and the row after the last visible one (empty when equal).</returns>
    public (long First, long End) VisibleRows(double scrollY, double bodyHeight, long rowCount)
    {
        if (rowCount <= 0 || bodyHeight <= 0)
        {
            return (0, 0);
        }

        var first = Math.Clamp((long)Math.Floor(scrollY / RowHeight), 0, rowCount);
        var end = Math.Clamp((long)Math.Ceiling((scrollY + bodyHeight) / RowHeight), first, rowCount);
        return (first, end);
    }

    /// <summary>Gets the columns that intersect the viewport.</summary>
    /// <param name="scrollX">The horizontal scroll offset.</param>
    /// <param name="viewportWidth">The visible width.</param>
    /// <returns>The first visible column and the column after the last visible one (empty when equal).</returns>
    public (int First, int End) VisibleColumns(double scrollX, double viewportWidth)
    {
        if (ColumnCount == 0 || viewportWidth <= 0 || scrollX >= TotalWidth)
        {
            return (0, 0);
        }

        var first = ColumnAt(Math.Max(0, scrollX));
        var right = scrollX + viewportWidth;
        var end = first;
        while (end < ColumnCount && _lefts[end] < right)
        {
            end++;
        }

        return (first, end);
    }

    /// <summary>Finds the column containing a content x coordinate.</summary>
    /// <param name="contentX">The x coordinate relative to the content origin.</param>
    /// <returns>The column index, or -1 outside the columns.</returns>
    public int ColumnAt(double contentX)
    {
        if (contentX < 0 || contentX >= TotalWidth)
        {
            return -1;
        }

        var index = Array.BinarySearch(_lefts, 0, ColumnCount, contentX);
        return index >= 0 ? index : ~index - 1;
    }

    /// <summary>Finds the column whose right border lies within <paramref name="tolerance"/> of a viewport x coordinate.</summary>
    /// <param name="viewX">The x coordinate relative to the control.</param>
    /// <param name="scrollX">The horizontal scroll offset.</param>
    /// <param name="tolerance">The half-width of the grab zone.</param>
    /// <returns>The column to resize, or -1 when no border is close.</returns>
    public int ResizeHandleAt(double viewX, double scrollX, double tolerance)
    {
        var contentX = viewX + scrollX;
        var column = ColumnAt(contentX);
        if (column < 0)
        {
            return -1;
        }

        if (_lefts[column + 1] - contentX <= tolerance)
        {
            return column;
        }

        return column > 0 && contentX - _lefts[column] <= tolerance ? column - 1 : -1;
    }
}
