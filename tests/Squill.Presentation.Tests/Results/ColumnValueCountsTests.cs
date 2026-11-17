using System.Diagnostics;
using Squill.Core;
using Squill.Presentation.Results;
using Xunit;

namespace Squill.Presentation.Tests.Results;

public sealed class ColumnValueCountsTests
{
    private sealed class ListSource(Type type, IReadOnlyList<object?> values) : IGridDataSource
    {
        public IReadOnlyList<ColumnInfo> Columns { get; } = [new("c", "x", type, true, 0)];

        public long RowCount => values.Count;

        public bool IsComplete => true;

        public bool IsTruncated => false;

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public object? GetCell(long row, int column) => values[(int)row];
    }

    [Fact]
    public void Counts_OrderedByCountThenValue_NullIsItsOwnEntry()
    {
        var source = new ListSource(typeof(string), ["b", "a", null, "b", "a", "c", null, "b"]);
        var (values, truncated) = ColumnValueCounts.Compute(source, 0, 10, CancellationToken.None);
        Assert.False(truncated);
        Assert.Equal([("b", 3), (null, 2), ("a", 2), ("c", 1)], values.Select(v => (v.Value, v.Count)).ToArray()!);
    }

    [Fact]
    public void TextSpellings_AreDistinctValues()
    {
        var source = new ListSource(typeof(string), ["Lyon", "lyon", "Lyon"]);
        var (values, _) = ColumnValueCounts.Compute(source, 0, 10, CancellationToken.None);
        Assert.Equal(2, values.Count);
        Assert.Equal(new ValueCount("Lyon", 2), values[0]);
    }

    [Fact]
    public void ByteArrays_AreCountedByContent()
    {
        var source = new ListSource(typeof(byte[]), [new byte[] { 1 }, new byte[] { 1 }]);
        var (values, _) = ColumnValueCounts.Compute(source, 0, 10, CancellationToken.None);
        Assert.Equal(2, Assert.Single(values).Count);
    }

    [Fact]
    public void Cap_TruncatesAndFlags()
    {
        var source = new ListSource(typeof(int), Enumerable.Range(0, 20).Cast<object?>().ToArray());
        var (values, truncated) = ColumnValueCounts.Compute(source, 0, 5, CancellationToken.None);
        Assert.True(truncated);
        Assert.Equal(5, values.Count);
        Assert.False(ColumnValueCounts.Compute(source, 0, 20, CancellationToken.None).Truncated);
    }

    [Fact]
    public void Cancellation_StopsTheScan()
    {
        var source = new ListSource(typeof(int), new object?[10_000]);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => ColumnValueCounts.Compute(source, 0, 10, cts.Token));
    }

    [Fact]
    public void HundredThousandRows_AreCountedQuickly()
    {
        var source = new ListSource(typeof(int), Enumerable.Range(0, 100_000).Select(i => (object?)(i % 1000)).ToArray());
        var watch = Stopwatch.StartNew();
        var (values, _) = ColumnValueCounts.Compute(source, 0, 500, CancellationToken.None);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"took {watch.Elapsed}");
        Assert.Equal(500, values.Count);
        Assert.All(values, v => Assert.Equal(100, v.Count));
    }
}
