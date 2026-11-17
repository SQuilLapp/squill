using Squill.Core;
using Squill.Presentation.Results;
using Xunit;

namespace Squill.Presentation.Tests.Results;

public sealed class GridSortTests
{
    private static ColumnInfo Col(Type t) => new("c", "x", t, true, 0);

    private static object?[] Sort(Type type, bool descending, params object?[] values)
    {
        var copy = (object?[])values.Clone();
        Array.Sort(copy, GridSortComparer.Create(Col(type), descending));
        return copy;
    }

    [Fact]
    public void Number_AcrossClrTypes_NullFirstAscendingLastDescending()
    {
        Assert.Equal([null, 1, 2L, 3.5m], Sort(typeof(decimal), false, 3.5m, null, 2L, 1));
        Assert.Equal([3.5m, 2L, 1, null], Sort(typeof(decimal), true, 3.5m, null, 2L, 1));
    }

    [Fact]
    public void Moment_SortsChronologically()
    {
        var a = new DateTime(2020, 1, 1);
        var b = new DateTime(2021, 1, 1);
        Assert.Equal([a, b], Sort(typeof(DateTime), false, b, a));
        Assert.Equal([b, a], Sort(typeof(DateTime), true, a, b));
    }

    [Fact]
    public void Boolean_FalseBeforeTrue() => Assert.Equal([null, false, true], Sort(typeof(bool), false, true, null, false));

    [Fact]
    public void Text_IgnoresCase() => Assert.Equal(["a", "B", "c"], Sort(typeof(string), false, "c", "B", "a"));

    [Fact]
    public void Bytes_ByLengthThenContent() =>
        Assert.Equal([new byte[] { 9 }, new byte[] { 1, 1 }, new byte[] { 1, 2 }], Sort(typeof(byte[]), false, new byte[] { 1, 2 }, new byte[] { 9 }, new byte[] { 1, 1 }));

    [Fact]
    public void EqualKeys_CompareAsZero()
    {
        Assert.Equal(0, GridSortComparer.Create(Col(typeof(int)), false).Compare(5, 5));
        Assert.Equal(0, GridSortComparer.Create(Col(typeof(string)), true).Compare("A", "a"));
        Assert.Equal(0, GridSortComparer.Create(Col(typeof(int)), false).Compare(null, null));
    }

    [Fact]
    public void MixedClrTypes_DoNotThrow()
    {
        var comparer = GridSortComparer.Create(Col(typeof(int)), false);
        var ex = Record.Exception(() => comparer.Compare(1, "abc"));
        Assert.Null(ex);
        Assert.Null(Record.Exception(() => comparer.Compare(new object(), Guid.NewGuid())));
    }

    [Fact]
    public void Next_CyclesAscendingDescendingNone()
    {
        var first = GridSort.Next(null, 2);
        Assert.Equal(new GridSort(2, false), first);
        var second = GridSort.Next(first, 2);
        Assert.Equal(new GridSort(2, true), second);
        Assert.Null(GridSort.Next(second, 2));
        Assert.Equal(new GridSort(3, false), GridSort.Next(second, 3));
    }
}
