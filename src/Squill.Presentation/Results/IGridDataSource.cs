using Squill.Core;

namespace Squill.Presentation.Results;

/// <summary>Random-access, possibly still growing, view of a result set for a virtualizing grid.</summary>
public interface IGridDataSource
{
    /// <summary>The result columns.</summary>
    IReadOnlyList<ColumnInfo> Columns { get; }

    /// <summary>The number of rows loaded so far.</summary>
    long RowCount { get; }

    /// <summary>Gets whether every row has been read (or the row limit reached).</summary>
    bool IsComplete { get; }

    /// <summary>Gets whether reading stopped at the row limit while more rows were available.</summary>
    bool IsTruncated { get; }

    /// <summary>Reads one cell.</summary>
    /// <param name="row">Zero-based row index.</param>
    /// <param name="column">Zero-based column index.</param>
    /// <returns>The cell value, or <c>null</c> for SQL NULL and for positions outside the loaded range.</returns>
    object? GetCell(long row, int column);

    /// <summary>Raised on the UI thread after <see cref="RowCount"/> or the completion state changed.</summary>
    event EventHandler? Changed;
}
