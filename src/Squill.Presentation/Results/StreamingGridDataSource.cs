using System.Diagnostics;
using Squill.Core;
using Squill.Data;

namespace Squill.Presentation.Results;

/// <summary>Grid data source that appends the batches of an <see cref="IResultReader"/> without copying the row arrays.</summary>
public sealed class StreamingGridDataSource : IGridDataSource
{
    private const int BatchSize = 1000;
    private const int ChunkShift = 12;
    private const int ChunkSize = 1 << ChunkShift;
    private static readonly long ThrottleTicks = Stopwatch.Frequency / 20;

    private readonly IResultReader _reader;
    private readonly int _maxRows;
    private readonly IUiDispatcher _dispatcher;
    private readonly Lock _gate = new();
    private readonly List<object?[][]> _chunks = [];
    private long _rowCount;
    private volatile bool _isComplete;
    private volatile bool _isTruncated;
    private long _lastRaise;

    /// <summary>Creates a data source over <paramref name="reader"/>.</summary>
    /// <param name="reader">The reader to drain; disposed when <see cref="StartAsync"/> ends.</param>
    /// <param name="maxRows">The maximum number of rows to keep.</param>
    /// <param name="dispatcher">Used to raise <see cref="Changed"/> on the UI thread.</param>
    public StreamingGridDataSource(IResultReader reader, int maxRows, IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRows);
        _reader = reader;
        _maxRows = maxRows;
        _dispatcher = dispatcher;
    }

    /// <inheritdoc/>
    public IReadOnlyList<ColumnInfo> Columns => _reader.Columns;

    /// <inheritdoc/>
    public long RowCount => Volatile.Read(ref _rowCount);

    /// <inheritdoc/>
    public bool IsComplete => _isComplete;

    /// <inheritdoc/>
    public bool IsTruncated => _isTruncated;

    /// <inheritdoc/>
    public event EventHandler? Changed;

    /// <summary>Reads the whole result (up to the row limit), raising <see cref="Changed"/> at most every 50 ms and once at the end.</summary>
    /// <param name="cancellationToken">Cancels the read; the rows loaded so far stay available.</param>
    /// <returns>A task that completes when the reader is drained or the limit is reached.</returns>
    /// <exception cref="OperationCanceledException">The read was cancelled.</exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (RowCount < _maxRows)
            {
                var want = (int)Math.Min(BatchSize, _maxRows - RowCount);
                if (await _reader.ReadBatchAsync(want, cancellationToken) is not { } batch)
                    break;
                Append(batch);
                var now = Stopwatch.GetTimestamp();
                if (now - _lastRaise >= ThrottleTicks)
                {
                    _lastRaise = now;
                    RaiseChanged();
                }
            }

            if (RowCount >= _maxRows)
                _isTruncated = await _reader.ReadBatchAsync(1, cancellationToken) is not null;
            _isComplete = true;
        }
        finally
        {
            await _reader.DisposeAsync();
            RaiseChanged();
        }
    }

    /// <inheritdoc/>
    public object? GetCell(long row, int column)
    {
        if (row < 0 || column < 0)
            return null;
        lock (_gate)
        {
            if (row >= _rowCount)
                return null;
            var values = _chunks[(int)(row >> ChunkShift)][row & (ChunkSize - 1)];
            return column < values.Length ? values[column] : null;
        }
    }

    private void Append(object?[][] batch)
    {
        lock (_gate)
        {
            foreach (var row in batch)
            {
                var index = (int)(_rowCount & (ChunkSize - 1));
                if (index == 0)
                    _chunks.Add(new object?[ChunkSize][]);
                _chunks[^1][index] = row;
                Volatile.Write(ref _rowCount, _rowCount + 1);
            }
        }
    }

    private void RaiseChanged() => _dispatcher.Post(() => Changed?.Invoke(this, EventArgs.Empty));
}
