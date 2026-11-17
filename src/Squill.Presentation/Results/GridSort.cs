using Squill.Core;

namespace Squill.Presentation.Results;

/// <summary>The sort of a grid: one column, ascending or descending.</summary>
/// <param name="Column">Zero-based index of the sorted column.</param>
/// <param name="Descending">Whether the order is descending.</param>
public sealed record GridSort(int Column, bool Descending)
{
    /// <summary>Applies one click on a column header to the current sort.</summary>
    /// <param name="current">The current sort, <see langword="null"/> for none.</param>
    /// <param name="column">The clicked column.</param>
    /// <returns>Ascending for a new column, then descending, then <see langword="null"/> (no sort) on the same column.</returns>
    public static GridSort? Next(GridSort? current, int column) => current is null || current.Column != column
        ? new GridSort(column, false)
        : current.Descending ? null : current with { Descending = true };
}

/// <summary>Builds typed comparers for sorting grid rows by one column.</summary>
public static class GridSortComparer
{
    /// <summary>Creates the comparer of a column.</summary>
    /// <param name="column">The sorted column; its type category decides the comparison.</param>
    /// <param name="descending">Whether to reverse the order (NULL then comes last).</param>
    /// <returns>A comparer that never throws: values of an unexpected CLR type are compared as formatted text. Equal keys compare as 0.</returns>
    public static IComparer<object?> Create(ColumnInfo column, bool descending)
    {
        ArgumentNullException.ThrowIfNull(column);
        return new Comparer(column, descending);
    }

    private sealed class Comparer(ColumnInfo column, bool descending) : IComparer<object?>
    {
        public int Compare(object? x, object? y)
        {
            var order = Ascending(x, y);
            return descending ? -order : order;
        }

        private int Ascending(object? x, object? y)
        {
            if (x is null || y is null)
                return x is null ? (y is null ? 0 : -1) : 1;
            switch (GridFilterMatcher.CategoryOf(column))
            {
                case GridFilterMatcher.Category.Number or GridFilterMatcher.Category.Moment or GridFilterMatcher.Category.Boolean
                    when GridFilterMatcher.CompiledGridFilter.TryCompare(x, y, out var order):
                    return order;
                case GridFilterMatcher.Category.Other when x is byte[] a && y is byte[] b:
                    return a.Length != b.Length ? a.Length.CompareTo(b.Length) : a.AsSpan().SequenceCompareTo(b);
                default:
                    return StringComparer.CurrentCultureIgnoreCase.Compare(CellFormatter.Format(x, column), CellFormatter.Format(y, column));
            }
        }
    }
}
