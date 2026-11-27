using Squill.Core;

namespace Squill.Data.Informix;

/// <summary>The result of a statement the connection handled itself (a transaction command): no columns, no rows.</summary>
internal sealed class NoResultReader : IResultReader
{
    /// <inheritdoc />
    public IReadOnlyList<ColumnInfo> Columns => [];

    /// <inheritdoc />
    public long RecordsAffected => 0;

    /// <inheritdoc />
    public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken) => ValueTask.FromResult<object?[][]?>(null);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
