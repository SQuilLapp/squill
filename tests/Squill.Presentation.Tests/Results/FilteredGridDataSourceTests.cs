using System.Diagnostics;
using Squill.Core;
using Squill.Presentation.Results;
using Xunit;

namespace Squill.Presentation.Tests.Results;

public sealed class FilteredGridDataSourceTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(60);

    private sealed class FakeSource : IGridDataSource
    {
        private readonly List<object?[]> _rows = [];
        private readonly Lock _gate = new();
        private volatile bool _complete;

        public IReadOnlyList<ColumnInfo> Columns { get; } =
        [
            new("id", "int", typeof(int), false, 0),
            new("name", "text", typeof(string), true, 1),
        ];

        public Action? OnRead { get; set; }

        public long RowCount
        {
            get
            {
                lock (_gate)
                    return _rows.Count;
            }
        }

        public bool IsComplete => _complete;

        public bool IsTruncated => false;

        public event EventHandler? Changed;

        public void Add(params object?[][] rows)
        {
            lock (_gate)
                _rows.AddRange(rows);
        }

        public void Raise(bool complete = false)
        {
            _complete = complete;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public object? GetCell(long row, int column)
        {
            OnRead?.Invoke();
            lock (_gate)
                return row >= 0 && row < _rows.Count ? _rows[(int)row][column] : null;
        }
    }

    private static FakeSource Names(params string?[] names)
    {
        var source = new FakeSource();
        source.Add([.. names.Select((n, i) => new object?[] { i, n })]);
        source.Raise(true);
        return source;
    }

    private static GridFilter Contains(FakeSource s, string text)
    {
        Assert.True(GridFilterMatcher.TryCreate(s.Columns[1], GridFilterOperator.Contains, text, out var f, out _));
        return f;
    }

    private static async Task<long[]> Rows(FilteredGridDataSource view)
    {
        await view.WhenIdle().WaitAsync(Bound, TestContext.Current.CancellationToken);
        return [.. Enumerable.Range(0, (int)view.RowCount).Select(i => view.SourceRow(i))];
    }

    private static async Task<string> Settle(FilteredGridDataSource view) => string.Join(",", await Rows(view));

    private static FilteredGridDataSource Create(FakeSource source) => new(source, new ImmediateDispatcher());

    [Fact]
    public async Task Filter_KeepsMatchingRowsInSourceOrder()
    {
        var source = Names("Lyon", "Paris", "Lille", "Nice");
        using var view = Create(source);
        view.Apply([Contains(source, "l")], null, null);
        Assert.Equal("0,2", await Settle(view));
        Assert.Equal("Lille", view.GetCell(1, 1));
    }

    [Fact]
    public async Task Search_MatchesAnyColumnIgnoringCase()
    {
        var source = Names("Lyon", "Paris", "Lille");
        using var view = Create(source);
        view.Apply([], "PARIS", null);
        Assert.Equal("1", await Settle(view));
        view.Apply([], "2", null);
        Assert.Equal("2", await Settle(view));
    }

    [Fact]
    public async Task FilterAndSearch_AreCombinedWithAnd()
    {
        var source = Names("Lyon", "Paris", "Lille", "Nice");
        using var view = Create(source);
        view.Apply([Contains(source, "l")], "lyo", null);
        Assert.Equal("0", await Settle(view));
    }

    [Fact]
    public async Task Sort_OrdersByColumnAndIsStable()
    {
        var source = Names("b", "a", "b", null, "a");
        using var view = Create(source);
        view.Apply([], null, new GridSort(1, false));
        Assert.Equal("3,1,4,0,2", await Settle(view));
        view.Apply([], null, new GridSort(1, true));
        Assert.Equal("0,2,1,4,3", await Settle(view));
    }

    [Fact]
    public async Task FilterAndSort_Combine()
    {
        var source = Names("b2", "a1", "c", "a3");
        using var view = Create(source);
        view.Apply([Contains(source, "a")], null, new GridSort(1, true));
        Assert.Equal("3,1", await Settle(view));
    }

    [Fact]
    public async Task SourceRow_MapsEveryViewRowToTheRightInnerRow()
    {
        var source = Names("x", "y", "x", "z", "x");
        using var view = Create(source);
        view.Apply([Contains(source, "x")], null, null);
        await Settle(view);
        for (var i = 0; i < view.RowCount; i++)
            Assert.Equal((int)view.SourceRow(i), view.GetCell(i, 0));
        Assert.Equal(-1, view.SourceRow(view.RowCount));
        Assert.Equal(-1, view.SourceRow(-1));
        Assert.Null(view.GetCell(view.RowCount, 0));
    }

    [Fact]
    public async Task Apply_NoMatch_GivesEmptyView()
    {
        var source = Names("a", "b");
        using var view = Create(source);
        view.Apply([], "zzz", null);
        Assert.Empty(await Rows(view));
        Assert.Equal(2, view.SourceRowCount);
    }

    [Fact]
    public async Task EmptyApply_IsPassThrough()
    {
        var source = Names("a", "b", "c");
        using var view = Create(source);
        view.Apply([Contains(source, "a")], null, null);
        await Settle(view);
        view.Apply([], "  ", null);
        Assert.Equal(3, view.RowCount);
        Assert.Equal(2, view.SourceRow(2));
        Assert.Equal(-1, view.SourceRow(3));
        Assert.Equal("c", view.GetCell(2, 1));
    }

    [Fact]
    public async Task StreamingRows_AppearUnsorted_AndAreSortedAfterCompletion()
    {
        var source = new FakeSource();
        source.Add([2, "b"], [1, "a"]);
        using var view = Create(source);
        view.Apply([], null, new GridSort(1, false));
        Assert.Equal("1,0", await Settle(view));

        source.Add([3, "0"], [4, "z"]);
        source.Raise();
        await Settle(view);
        source.Raise(true);
        // The refresh throttle is at most one second; the completion rebuild is always published.
        var done = new TaskCompletionSource();
        view.Changed += (_, _) =>
        {
            if (view.SourceRow(0) == 2)
                done.TrySetResult();
        };
        if (view.SourceRow(0) != 2)
            await done.Task.WaitAsync(Bound, TestContext.Current.CancellationToken);
        Assert.Equal("2,1,0,3", await Settle(view));
    }

    [Fact]
    public async Task StreamingRows_WithoutSort_AreAppendedIncrementally()
    {
        var source = new FakeSource();
        source.Add([1, "a"], [2, "b"]);
        using var view = Create(source);
        view.Apply([Contains(source, "a")], null, null);
        Assert.Equal("0", await Settle(view));
        source.Add([3, "ab"], [4, "c"]);
        source.Raise(true);
        Assert.Equal("0,2", await Settle(view));
    }

    [Fact]
    public async Task Apply_Twice_OnlySecondResultIsPublished()
    {
        var source = Names("a", "b", "a", "b");
        using var view = Create(source);
        using var gate = new ManualResetEventSlim();
        var entered = new TaskCompletionSource();
        var first = 0;
        source.OnRead = () =>
        {
            if (Interlocked.Increment(ref first) != 1)
                return;
            entered.SetResult();
            gate.Wait(Bound, TestContext.Current.CancellationToken);
        };
        view.Apply([Contains(source, "a")], null, null);
        await entered.Task.WaitAsync(Bound, TestContext.Current.CancellationToken);
        view.Apply([Contains(source, "b")], null, null);
        // The second rebuild runs while the first is still held; release the first afterwards.
        while (view.RowCount != 2 || view.SourceRow(0) != 1)
            await Task.Delay(1, TestContext.Current.CancellationToken);
        gate.Set();
        Assert.Equal("1,3", await Settle(view));
    }

    [Fact]
    public async Task Dispose_StopsWork_AndUnsubscribes()
    {
        var source = new FakeSource();
        source.Add([1, "a"]);
        var view = Create(source);
        view.Apply([Contains(source, "a")], null, null);
        await Settle(view);
        view.Dispose();
        var raised = 0;
        view.Changed += (_, _) => raised++;
        source.Add([2, "a"]);
        source.Raise(true);
        await view.WhenIdle().WaitAsync(Bound, TestContext.Current.CancellationToken);
        Assert.Equal(0, raised);
        Assert.Equal(1, view.RowCount);
    }

    [Fact]
    public async Task LargeResult_ApplyReturnsImmediately_AndFilterAndSortFinish()
    {
        var source = new FakeSource();
        source.Add([.. Enumerable.Range(0, 200_000).Select(i => new object?[] { i, "n" + (i % 1000) })]);
        source.Raise(true);
        using var view = Create(source);
        var watch = Stopwatch.StartNew();
        view.Apply([Contains(source, "n1")], "9", new GridSort(0, true));
        var applied = watch.Elapsed;
        var rows = await Rows(view);
        Assert.True(applied < TimeSpan.FromSeconds(1));
        Assert.NotEmpty(rows);
        Assert.Equal(rows.OrderByDescending(r => r).ToArray(), rows);
    }
}
