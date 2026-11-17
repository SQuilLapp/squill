using System.Diagnostics;
using Squill.Core;

namespace Squill.Presentation.Results;

/// <summary>
/// Read-only <see cref="IGridDataSource"/> over another one that shows only the rows matching filters and a search text,
/// in the order of an optional sort, and follows a source that is still streaming.
/// </summary>
/// <remarks>
/// Rebuilds run on a worker and publish one immutable snapshot at a time; a rebuild started by an older <see cref="Apply"/>
/// never publishes. <see cref="Changed"/> raises are coalesced: at most one is queued on the dispatcher at a time.
/// </remarks>
public sealed class FilteredGridDataSource : IGridDataSource, IDisposable
{
    private const int ChunkShift = 12;
    private const int ChunkSize = 1 << ChunkShift;
    private const int CancelCheckInterval = 4096;
    private static readonly long SortedRefreshTicks = Stopwatch.Frequency;

    private readonly IGridDataSource _inner;
    private readonly IUiDispatcher _dispatcher;
    private readonly Lock _lock = new();
    private readonly List<Task> _tasks = [];
    private Spec _spec = Spec.None;
    private Snapshot? _snapshot;
    private CancellationTokenSource? _cts;
    private int _generation;
    private bool _running;
    private bool _dirty;
    private bool _raisePending;
    private bool _disposed;

    /// <summary>Creates a view over <paramref name="inner"/>; with no <see cref="Apply"/> it is a pass-through.</summary>
    /// <param name="inner">The source to filter; its <see cref="IGridDataSource.GetCell"/> must be callable from a worker thread.</param>
    /// <param name="dispatcher">Used to raise <see cref="Changed"/> on the UI thread.</param>
    public FilteredGridDataSource(IGridDataSource inner, IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(dispatcher);
        _inner = inner;
        _dispatcher = dispatcher;
        _inner.Changed += OnInnerChanged;
    }

    /// <inheritdoc/>
    public IReadOnlyList<ColumnInfo> Columns => _inner.Columns;

    /// <inheritdoc/>
    public long RowCount => Current is { } s ? s.Count : _inner.RowCount;

    /// <summary>Gets the number of rows of the inner source, for a footer such as "12 of 3,400".</summary>
    public long SourceRowCount => _inner.RowCount;

    /// <inheritdoc/>
    public bool IsComplete => _inner.IsComplete;

    /// <inheritdoc/>
    public bool IsTruncated => _inner.IsTruncated;

    /// <inheritdoc/>
    public event EventHandler? Changed;

    /// <inheritdoc/>
    public object? GetCell(long row, int column)
    {
        var source = SourceRow(row);
        return source < 0 ? null : _inner.GetCell(source, column);
    }

    /// <summary>Maps a view row to the row of the inner source.</summary>
    /// <param name="viewRow">Zero-based view row.</param>
    /// <returns>The inner row, or -1 outside the view.</returns>
    public long SourceRow(long viewRow)
    {
        if (viewRow < 0)
            return -1;
        if (Current is not { } s)
            return viewRow < _inner.RowCount ? viewRow : -1;
        return viewRow < s.Count ? s.Chunks[(int)(viewRow >> ChunkShift)][viewRow & (ChunkSize - 1)] : -1;
    }

    /// <summary>Sets the view and rebuilds it in the background; returns at once.</summary>
    /// <param name="filters">The filters, combined with AND.</param>
    /// <param name="search">Case-insensitive text that must appear in the formatted text of some column; empty or whitespace means none.</param>
    /// <param name="sort">The order, or <see langword="null"/> for source order.</param>
    /// <exception cref="ArgumentOutOfRangeException">A filter or the sort names a column that does not exist.</exception>
    public void Apply(IReadOnlyList<GridFilter> filters, string? search, GridSort? sort)
    {
        ArgumentNullException.ThrowIfNull(filters);
        var hasFilters = filters.Count > 0;
        var columns = _inner.Columns;
        var compiled = new GridFilterMatcher.CompiledGridFilter[filters.Count];
        var filterColumns = new int[filters.Count];
        for (var i = 0; i < compiled.Length; i++)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(filters[i].Column);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(filters[i].Column, columns.Count);
            compiled[i] = GridFilterMatcher.Compile(filters[i], columns[filters[i].Column]);
            filterColumns[i] = filters[i].Column;
        }

        if (sort is not null)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sort.Column);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(sort.Column, columns.Count);
        }

        var text = string.IsNullOrWhiteSpace(search) ? null : search;
        var spec = !hasFilters && text is null && sort is null ? Spec.None : new Spec(compiled, filterColumns, text, sort);
        CancellationToken token;
        int generation;
        lock (_lock)
        {
            if (_disposed)
                return;
            _cts?.Cancel();
            _cts?.Dispose();
            _generation++;
            generation = _generation;
            _spec = spec;
            _dirty = false;
            if (spec.IsNone)
            {
                _cts = null;
                _running = false;
                _snapshot = null;
                token = default;
            }
            else
            {
                _cts = new CancellationTokenSource();
                token = _cts.Token;
                _running = true;
                _tasks.RemoveAll(t => t.IsCompleted);
                _tasks.Add(Task.Run(() => RunAsync(generation, spec, token), CancellationToken.None));
            }
        }

        if (spec.IsNone)
            RaiseChanged();
    }

    /// <summary>Cancels the running rebuild and detaches from the inner source.</summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;
            _disposed = true;
            _generation++;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        _inner.Changed -= OnInnerChanged;
    }

    /// <summary>Gets a task that completes when every rebuild started so far has ended (for tests).</summary>
    /// <returns>The combined task.</returns>
    internal Task WhenIdle()
    {
        lock (_lock)
            return Task.WhenAll(_tasks);
    }

    private Snapshot? Current
    {
        get
        {
            lock (_lock)
                return _snapshot;
        }
    }

    private void OnInnerChanged(object? sender, EventArgs e)
    {
        Spec spec;
        int generation;
        CancellationToken token;
        lock (_lock)
        {
            if (_disposed)
                return;
            spec = _spec;
            if (spec.IsNone)
            {
                _dispatcher.Post(() => Changed?.Invoke(this, EventArgs.Empty));
                return;
            }

            if (_running)
            {
                _dirty = true;
                return;
            }

            _running = true;
            generation = _generation;
            token = _cts!.Token;
            _tasks.RemoveAll(t => t.IsCompleted);
            _tasks.Add(Task.Run(() => RunAsync(generation, spec, token), CancellationToken.None));
        }
    }

    private async Task RunAsync(int generation, Spec spec, CancellationToken token)
    {
        try
        {
            var lastPublish = 0L;
            while (true)
            {
                var complete = _inner.IsComplete;
                var count = _inner.RowCount;
                var previous = Current;
                if (previous is not null && previous.Generation == generation && previous.Scanned == count)
                {
                    // Nothing new to scan, but the footer may still need the new state.
                    Raise();
                }
                else
                {
                    if (spec.Sort is not null && !complete && lastPublish != 0)
                    {
                        var wait = SortedRefreshTicks - (Stopwatch.GetTimestamp() - lastPublish);
                        if (wait > 0)
                        {
                            await Task.Delay(TimeSpan.FromSeconds((double)wait / Stopwatch.Frequency), token);
                            complete = _inner.IsComplete;
                            count = _inner.RowCount;
                        }
                    }

                    var snapshot = spec.Sort is null && previous is not null && previous.Generation == generation
                        ? Append(previous, spec, count, token)
                        : Build(generation, spec, count, token);
                    lock (_lock)
                    {
                        if (generation != _generation)
                            return;
                        _snapshot = snapshot with { Generation = generation };
                    }

                    lastPublish = Stopwatch.GetTimestamp();
                    Raise();
                }

                lock (_lock)
                {
                    if (generation != _generation)
                        return;
                    if (!_dirty)
                    {
                        _running = false;
                        return;
                    }

                    _dirty = false;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer Apply or disposed: the result must not be published.
        }

        void Raise()
        {
            lock (_lock)
                if (generation != _generation)
                    return;
            RaiseChanged();
        }
    }

    private Snapshot Append(Snapshot previous, Spec spec, long count, CancellationToken token)
    {
        var builder = new Builder(previous.Chunks, previous.Count);
        for (var row = previous.Scanned; row < count; row++)
        {
            if ((row & (CancelCheckInterval - 1)) == 0)
                token.ThrowIfCancellationRequested();
            if (Matches(spec, row))
                builder.Add(row);
        }

        return builder.ToSnapshot(count);
    }

    private Snapshot Build(int generation, Spec spec, long count, CancellationToken token)
    {
        var builder = new Builder([], 0);
        if (spec.Sort is null)
        {
            for (var row = 0L; row < count; row++)
            {
                if ((row & (CancelCheckInterval - 1)) == 0)
                    token.ThrowIfCancellationRequested();
                if (Matches(spec, row))
                    builder.Add(row);
            }

            return builder.ToSnapshot(count);
        }

        var rows = new List<long>();
        var keys = new List<object?>();
        for (var row = 0L; row < count; row++)
        {
            if ((row & (CancelCheckInterval - 1)) == 0)
                token.ThrowIfCancellationRequested();
            if (!Matches(spec, row))
                continue;
            rows.Add(row);
            keys.Add(_inner.GetCell(row, spec.Sort.Column));
        }

        var comparer = GridSortComparer.Create(_inner.Columns[spec.Sort.Column], spec.Sort.Descending);
        var order = new int[rows.Count];
        for (var i = 0; i < order.Length; i++)
            order[i] = i;
        var compares = 0;
        Array.Sort(order, (a, b) =>
        {
            if ((++compares & (CancelCheckInterval - 1)) == 0)
                token.ThrowIfCancellationRequested();
            var result = comparer.Compare(keys[a], keys[b]);
            return result != 0 ? result : a.CompareTo(b); // ties keep source order: the sort is stable
        });
        foreach (var i in order)
            builder.Add(rows[i]);
        return builder.ToSnapshot(count);
    }

    private bool Matches(Spec spec, long row)
    {
        for (var i = 0; i < spec.Filters.Length; i++)
            if (!spec.Filters[i].Matches(_inner.GetCell(row, spec.FilterColumns[i])))
                return false;
        if (spec.Search is not { } search)
            return true;
        var columns = _inner.Columns;
        for (var c = 0; c < columns.Count; c++)
            if (CellFormatter.Format(_inner.GetCell(row, c), columns[c])?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
                return true;
        return false;
    }

    private void RaiseChanged()
    {
        lock (_lock)
        {
            if (_raisePending || _disposed)
                return;
            _raisePending = true;
        }

        _dispatcher.Post(() =>
        {
            lock (_lock)
                _raisePending = false;
            Changed?.Invoke(this, EventArgs.Empty);
        });
    }

    private sealed record Spec(GridFilterMatcher.CompiledGridFilter[] Filters, int[] FilterColumns, string? Search, GridSort? Sort)
    {
        public static Spec None { get; } = new([], [], null, null);

        public bool IsNone => ReferenceEquals(this, None);
    }

    /// <summary>Immutable view of the index: readers only touch entries below <see cref="Count"/>.</summary>
    private sealed record Snapshot(long[][] Chunks, long Count, long Scanned, int Generation);

    private sealed class Builder(long[][] chunks, long count)
    {
        private long[][] _chunks = chunks;
        private long _count = count;

        public void Add(long row)
        {
            var index = (int)(_count & (ChunkSize - 1));
            if (index == 0)
            {
                // Copy-on-grow keeps the array a published snapshot holds untouched.
                Array.Resize(ref _chunks, _chunks.Length + 1);
                _chunks[^1] = new long[ChunkSize];
            }

            // Writing into the last chunk past the published count is invisible to readers.
            _chunks[^1][index] = row;
            _count++;
        }

        public Snapshot ToSnapshot(long scanned) => new(_chunks, _count, scanned, 0);
    }
}
