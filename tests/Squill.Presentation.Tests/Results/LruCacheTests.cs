using Squill.Presentation.Results;
using Xunit;

namespace Squill.Presentation.Tests.Results;

public sealed class LruCacheTests
{
    [Fact]
    public void EvictsLeastRecentlyUsedAndReportsRemoval()
    {
        var removed = new List<int>();
        var cache = new LruCache<string, int>(2, removed.Add);
        cache.Add("a", 1);
        cache.Add("b", 2);
        Assert.True(cache.TryGet("a", out _));
        cache.Add("c", 3);

        Assert.Equal(2, cache.Count);
        Assert.False(cache.TryGet("b", out _));
        Assert.True(cache.TryGet("a", out var a));
        Assert.Equal(1, a);
        Assert.Equal([2], removed);
    }

    [Fact]
    public void ReplacingAKeyRemovesTheOldValueAndClearRemovesAll()
    {
        var removed = new List<int>();
        var cache = new LruCache<string, int>(4, removed.Add);
        cache.Add("a", 1);
        cache.Add("a", 2);
        cache.Add("b", 3);
        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.Equal([1, 2, 3], removed.Order());
    }

    [Fact]
    public void PercentileUsesNearestRank()
    {
        var values = Enumerable.Range(1, 100).Select(i => (double)i).ToArray();
        Assert.Equal(95, GridBenchReport.Percentile(values, 0.95));
        Assert.Equal(100, GridBenchReport.Percentile(values, 1));
    }

    [Fact]
    public void SyntheticSourceHasTenPercentNulls()
    {
        var source = new SyntheticGridSource(1000, 3);
        var nulls = Enumerable.Range(0, 1000).Count(r => source.GetCell(r, 0) is null);
        Assert.InRange(nulls, 80, 120);
        Assert.Null(source.GetCell(1000, 0));
    }
}
