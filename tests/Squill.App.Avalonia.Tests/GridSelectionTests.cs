using Squill.App.Avalonia.Controls;
using Squill.Core;
using Squill.Presentation.Results;
using Xunit;

namespace Squill.App.Avalonia.Tests;

public sealed class GridSelectionTests
{
    private sealed class MemorySource(string[] names, object?[][] rows) : IGridDataSource
    {
        public IReadOnlyList<ColumnInfo> Columns { get; } = [.. names.Select((n, i) => new ColumnInfo(n, "t", typeof(object), true, i))];
        public long RowCount => rows.Length;
        public bool IsComplete => true;
        public bool IsTruncated => false;
        public object? GetCell(long row, int column) => rows[row][column];
        public event EventHandler? Changed { add { } remove { } }
    }

    private static readonly MemorySource Data = new(["a", "b", "c"], [[1, "x", null], [2, null, "y"], [3, "z", "w"]]);

    [Fact]
    public void ClickThenShiftClick_SelectsRectangle()
    {
        var s = new GridSelection();
        s.SelectCell(2, 2, false);
        s.SelectCell(0, 1, true);
        Assert.True(s.Contains(1, 1, 3));
        Assert.True(s.Contains(2, 2, 3));
        Assert.False(s.Contains(1, 0, 3));
    }

    [Fact]
    public void ColumnClick_SelectsWholeColumnsAndExtends()
    {
        var s = new GridSelection();
        s.SelectColumn(0, false);
        s.SelectColumn(1, true);
        Assert.True(s.ContainsColumn(1));
        Assert.True(s.Contains(2, 1, 3));
        Assert.False(s.Contains(0, 2, 3));
    }

    [Fact]
    public void SelectAll_CoversEveryCell()
    {
        var s = new GridSelection();
        s.SelectAll(3, 3);
        Assert.True(s.Contains(2, 2, 3));
        Assert.Equal(0, s.FirstRow(3));
        s.SelectAll(0, 3);
        Assert.False(s.HasSelection);
    }

    [Fact]
    public void Move_ClampsAndExtends()
    {
        var s = new GridSelection();
        s.Move(1, 1, 3, 3, false);
        Assert.Equal((0L, 0), (s.ActiveRow, s.ActiveColumn));
        s.Move(10, 10, 3, 3, false);
        Assert.Equal((2L, 2), (s.ActiveRow, s.ActiveColumn));
        s.Move(-1, 0, 3, 3, true);
        Assert.True(s.Contains(1, 2, 3) && s.Contains(2, 2, 3));
    }

    [Fact]
    public void ToTsv_Rectangle_HasNoHeaderAndNullsAreEmpty()
    {
        var s = new GridSelection();
        s.SelectCell(0, 1, false);
        s.SelectCell(1, 2, true);
        Assert.Equal("x\t\n\ty\n", s.ToTsv(Data));
    }

    [Fact]
    public void ToTsv_WholeColumns_StartsWithHeader()
    {
        var s = new GridSelection();
        s.SelectColumn(0, false);
        s.SelectColumn(1, true);
        Assert.Equal("a\tb\n1\tx\n2\t\n3\tz\n", s.ToTsv(Data));
    }
}
