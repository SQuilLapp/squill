using System.Collections;

namespace Squill.Presentation.Results;

/// <summary>One distinct value of a column and how many rows hold it.</summary>
/// <param name="Value">The typed value, <see langword="null"/> for the SQL NULL entry.</param>
/// <param name="Count">The number of rows holding the value.</param>
public sealed record ValueCount(object? Value, int Count);

/// <summary>Counts the distinct values of a grid column, for the value list of the column menu.</summary>
public static class ColumnValueCounts
{
    private const int CancellationCheckInterval = 4096;

    /// <summary>Scans the rows loaded so far on the calling thread.</summary>
    /// <param name="source">The grid data.</param>
    /// <param name="column">Zero-based column index.</param>
    /// <param name="cap">The maximum number of entries returned.</param>
    /// <param name="ct">Checked every 4,096 rows.</param>
    /// <returns>The values by count descending then by the column sort order (text compared case-sensitively for distinctness), and whether more than <paramref name="cap"/> distinct values existed.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled during the scan.</exception>
    public static (IReadOnlyList<ValueCount> Values, bool Truncated) Compute(IGridDataSource source, int column, int cap, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(cap);
        var rows = source.RowCount;
        var counts = new Dictionary<object, int>(ValueComparer.Instance);
        var nulls = 0;
        for (long row = 0; row < rows; row++)
        {
            if (row % CancellationCheckInterval == 0)
                ct.ThrowIfCancellationRequested();
            var value = source.GetCell(row, column);
            if (value is null)
                nulls++;
            else
                counts[value] = counts.GetValueOrDefault(value) + 1;
        }

        var list = new List<ValueCount>(counts.Count + 1);
        if (nulls > 0)
            list.Add(new ValueCount(null, nulls));
        foreach (var (value, count) in counts)
            list.Add(new ValueCount(value, count));
        var sorter = GridSortComparer.Create(source.Columns[column], false);
        list.Sort((a, b) => a.Count != b.Count ? b.Count.CompareTo(a.Count) : sorter.Compare(a.Value, b.Value));
        var truncated = list.Count > cap;
        if (truncated)
            list.RemoveRange(cap, list.Count - cap);
        return (list, truncated);
    }

    // Byte arrays are values, not references; everything else uses its own equality.
    private sealed class ValueComparer : IEqualityComparer<object>
    {
        internal static readonly ValueComparer Instance = new();

        public new bool Equals(object? x, object? y) =>
            x is byte[] a && y is byte[] b ? a.AsSpan().SequenceEqual(b) : object.Equals(x, y);

        public int GetHashCode(object obj) =>
            obj is byte[] bytes ? ((IStructuralEquatable)bytes).GetHashCode(EqualityComparer<byte>.Default) : obj.GetHashCode();
    }
}
