using Squill.Core;

namespace Squill.Data;

/// <summary>Streams the rows of one statement in batches; statements without a result set have no columns.</summary>
public interface IResultReader : IAsyncDisposable
{
    /// <summary>The columns of the current result set; empty when the statement returns no result set.</summary>
    IReadOnlyList<ColumnInfo> Columns { get; }

    /// <summary>
    /// Rows affected by the statement, or -1 until every result set has been read. With several result sets (or statements) it is the
    /// sum of the sets that report a count.
    /// </summary>
    long RecordsAffected { get; }

    /// <summary>Reads up to <paramref name="maxRows"/> rows, or returns <c>null</c> at the end.</summary>
    ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken);

    /// <summary>Server messages received so far, in order (SQL Server <c>PRINT</c>, PostgreSQL <c>RAISE NOTICE</c>, MySQL warnings); empty when the engine has none.</summary>
    IReadOnlyList<string> Messages => [];

    /// <summary>Skips the rest of the current result set and moves to the next one, refreshing <see cref="Columns"/>.</summary>
    /// <param name="cancellationToken">Cancels the move.</param>
    /// <returns><c>true</c> when another result set is now current; <c>false</c> when there is none.</returns>
    ValueTask<bool> NextResultAsync(CancellationToken cancellationToken) => ValueTask.FromResult(false);
}
