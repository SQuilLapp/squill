using Squill.Core;

namespace Squill.Data;

/// <summary>Streams the rows of one statement in batches; statements without a result set have no columns.</summary>
public interface IResultReader : IAsyncDisposable
{
    /// <summary>The result columns; empty when the statement returns no result set.</summary>
    IReadOnlyList<ColumnInfo> Columns { get; }

    /// <summary>Rows affected by the statement, or -1 until known.</summary>
    long RecordsAffected { get; }

    /// <summary>Reads up to <paramref name="maxRows"/> rows, or returns <c>null</c> at the end.</summary>
    ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken);
}
