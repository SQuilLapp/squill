using Xunit;
using Squill.App.Avalonia.Controls;

namespace Squill.App.Avalonia.Tests;

public sealed class GridLayoutTests
{
    private static GridLayout Make(params double[] widths)
    {
        var layout = new GridLayout(20, 30);
        layout.SetColumnWidths(widths);
        return layout;
    }

    [Fact]
    public void WidthsAreClamped()
    {
        var layout = Make(10, 100, 900);
        Assert.Equal(60, layout.ColumnWidth(0));
        Assert.Equal(100, layout.ColumnWidth(1));
        Assert.Equal(400, layout.ColumnWidth(2));
        Assert.Equal(560, layout.TotalWidth);
        Assert.Equal(160, layout.ColumnLeft(2));
    }

    [Fact]
    public void SetColumnWidthShiftsFollowingColumns()
    {
        var layout = Make(100, 100, 100);
        layout.SetColumnWidth(0, 150);
        Assert.Equal(150, layout.ColumnLeft(1));
        Assert.Equal(350, layout.TotalWidth);
        layout.SetColumnWidth(0, 5);
        Assert.Equal(60, layout.ColumnWidth(0));
        Assert.Equal(260, layout.TotalWidth);
    }

    [Theory]
    [InlineData(0, 100, 5, 0, 5)]
    [InlineData(10, 100, 100, 0, 6)]
    [InlineData(40, 100, 100, 2, 7)]
    [InlineData(1950, 100, 100, 97, 100)]
    [InlineData(5000, 100, 100, 100, 100)]
    public void VisibleRowsCoverViewport(double scrollY, double height, long rows, long first, long end)
    {
        var layout = Make(100);
        Assert.Equal((first, end), layout.VisibleRows(scrollY, height, rows));
    }

    [Fact]
    public void VisibleRowsEmptyWhenNoRowsOrNoHeight()
    {
        var layout = Make(100);
        Assert.Equal((0L, 0L), layout.VisibleRows(0, 100, 0));
        Assert.Equal((0L, 0L), layout.VisibleRows(0, 0, 10));
    }

    [Fact]
    public void VisibleRowsHandlesMillionRows()
    {
        var layout = Make(100);
        var (first, end) = layout.VisibleRows(19_999_990, 200, 1_000_000);
        Assert.Equal(999_999, first);
        Assert.Equal(1_000_000, end);
        Assert.Equal(20_000_000, layout.TotalHeight(1_000_000));
    }

    [Theory]
    [InlineData(0, 250, 0, 3)]
    [InlineData(100, 50, 1, 2)]
    [InlineData(99, 2, 0, 2)]
    [InlineData(250, 100, 2, 3)]
    [InlineData(300, 100, 0, 0)]
    public void VisibleColumnsCoverViewport(double scrollX, double width, int first, int end)
    {
        var layout = Make(100, 100, 100);
        Assert.Equal((first, end), layout.VisibleColumns(scrollX, width));
    }

    [Theory]
    [InlineData(-1, -1)]
    [InlineData(0, 0)]
    [InlineData(99.9, 0)]
    [InlineData(100, 1)]
    [InlineData(299.9, 2)]
    [InlineData(300, -1)]
    public void ColumnAtFindsColumn(double x, int expected)
    {
        Assert.Equal(expected, Make(100, 100, 100).ColumnAt(x));
    }

    [Fact]
    public void ColumnAtOnEmptyLayoutIsNone() => Assert.Equal(-1, new GridLayout(20, 30).ColumnAt(0));

    [Theory]
    [InlineData(98, 0, 0)]
    [InlineData(103, 0, 0)]
    [InlineData(50, 0, -1)]
    [InlineData(90, 0, -1)]
    [InlineData(98, 100, 1)]
    [InlineData(20, 100, -1)]
    [InlineData(200, 0, 1)]
    public void ResizeHandleFindsBorder(double viewX, double scrollX, int expected)
    {
        Assert.Equal(expected, Make(100, 100, 100).ResizeHandleAt(viewX, scrollX, 4));
    }

    [Fact]
    public void MaxScrollIsNeverNegative()
    {
        var layout = Make(100, 100);
        Assert.Equal(0, layout.MaxScrollX(500));
        Assert.Equal(100, layout.MaxScrollX(100));
        Assert.Equal(0, layout.MaxScrollY(3, 500));
        Assert.Equal(40, layout.MaxScrollY(10, 160));
    }
}
