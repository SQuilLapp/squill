using Squill.Core;
using Squill.Core.Editing;
using Squill.Presentation.Results;
using Xunit;

namespace Squill.Presentation.Tests.Results;

public sealed class EditableGridDataSourceTests
{
    private static EditTarget Target()
    {
        var defs = new[]
        {
            new ColumnDefinition("Id", "int", false, true, null, true),
            new ColumnDefinition("Name", "text", true, false, null),
            new ColumnDefinition("Data", "blob", true, false, null),
        };
        return new EditTarget("main", "T", [new EditColumn(0, defs[0]), new EditColumn(1, defs[1]), new EditColumn(2, defs[2])],
            ["Id"], new TableInfo("main", "T", TableKind.Table, defs, [], []));
    }

    private static Fake Source(int rows, bool complete = true) => new(rows, complete);

    private static EditableGridDataSource Make(int rows = 5, bool complete = true) => new(Source(rows, complete), Target());

    [Fact]
    public void EditAndEditBackClearsChange()
    {
        var g = Make();
        g.SetCell(1, 1, "x");
        Assert.Equal("x", g.GetCell(1, 1));
        Assert.Equal("n1", g.GetOriginal(1, 1));
        Assert.True(g.IsCellModified(1, 1));
        Assert.Equal(RowState.Modified, g.GetRowState(1));
        Assert.Equal(1, g.PendingCount);
        g.SetCell(1, 1, "n1");
        Assert.False(g.IsCellModified(1, 1));
        Assert.Equal(RowState.Unchanged, g.GetRowState(1));
        Assert.False(g.HasPendingChanges);
    }

    [Fact]
    public void TwoCellsOfOneRowCountOnce()
    {
        var g = Make();
        g.SetCell(2, 0, 99);
        g.SetCell(2, 1, null);
        Assert.Equal(1, g.PendingCount);
        g.SetCell(2, 0, 2);
        Assert.Equal(RowState.Modified, g.GetRowState(2));
        g.SetCell(2, 1, "n2");
        Assert.Equal(RowState.Unchanged, g.GetRowState(2));
    }

    [Fact]
    public void ByteArraysCompareByContent()
    {
        var g = Make();
        g.SetCell(0, 2, new byte[] { 1, 2 });
        Assert.True(g.IsCellModified(0, 2));
        g.AcceptChanges(new Dictionary<long, object?>());
        g.SetCell(0, 2, new byte[] { 1, 2, 3 });
        Assert.True(g.IsCellModified(0, 2));
        g.SetCell(0, 2, new byte[] { 1, 2 });
        Assert.False(g.IsCellModified(0, 2));
    }

    [Fact]
    public void AddAndEditInsertedRow()
    {
        var g = Make();
        var r = g.AddRow();
        Assert.Equal(5, r);
        Assert.Equal(6, g.RowCount);
        Assert.Equal(RowState.Inserted, g.GetRowState(r));
        Assert.False(g.IsPersistedRow(r));
        Assert.False(g.IsCellSet(r, 1));
        Assert.Null(g.GetCell(r, 1));
        g.SetCell(r, 1, "new");
        Assert.True(g.IsCellSet(r, 1));
        Assert.Equal("new", g.GetCell(r, 1));
        Assert.Null(g.GetOriginal(r, 1));
        Assert.Equal(1, g.PendingCount);
    }

    [Fact]
    public void DeleteBaseRowKeepsItVisible()
    {
        var g = Make();
        g.SetCell(1, 1, "x");
        g.DeleteRow(1);
        Assert.Equal(5, g.RowCount);
        Assert.Equal(RowState.Deleted, g.GetRowState(1));
        Assert.Equal("n1", g.GetCell(1, 1));
        Assert.Equal(1, g.PendingCount);
        Assert.Throws<InvalidOperationException>(() => g.SetCell(1, 1, "y"));
    }

    [Fact]
    public void DeleteInsertedRowShiftsLaterRows()
    {
        var g = Make(2);
        var a = g.AddRow();
        var b = g.AddRow();
        g.SetCell(b, 1, "second");
        g.DeleteRow(a);
        Assert.Equal(3, g.RowCount);
        Assert.Equal("second", g.GetCell(2, 1));
        Assert.Equal(RowState.Inserted, g.GetRowState(2));
    }

    [Fact]
    public void RevertAndDiscard()
    {
        var g = Make();
        g.SetCell(0, 1, "x");
        g.DeleteRow(1);
        var r = g.AddRow();
        g.RevertRow(0);
        g.RevertRow(1);
        Assert.Equal(RowState.Unchanged, g.GetRowState(0));
        Assert.Equal(RowState.Unchanged, g.GetRowState(1));
        Assert.Equal(1, g.PendingCount);
        g.RevertRow(r);
        Assert.Equal(5, g.RowCount);
        g.SetCell(3, 1, "z");
        g.AddRow();
        g.DiscardAll();
        Assert.False(g.HasPendingChanges);
        Assert.Equal(5, g.RowCount);
        Assert.Equal("n3", g.GetCell(3, 1));
    }

    [Fact]
    public void AcceptUpdateMakesNewOriginal()
    {
        var g = Make();
        g.SetCell(1, 1, "x");
        g.AcceptChanges(new Dictionary<long, object?>());
        Assert.False(g.HasPendingChanges);
        Assert.Equal("x", g.GetCell(1, 1));
        Assert.Equal("x", g.GetOriginal(1, 1));
        g.SetCell(1, 1, "n1");
        Assert.True(g.IsCellModified(1, 1));
        g.SetCell(1, 1, "x");
        Assert.False(g.IsCellModified(1, 1));
    }

    [Fact]
    public void AcceptDeleteRemovesRowsAndShifts()
    {
        var g = Make();
        g.DeleteRow(1);
        g.DeleteRow(3);
        g.AcceptChanges(new Dictionary<long, object?>());
        Assert.Equal(3, g.RowCount);
        Assert.Equal(0, g.GetCell(0, 0));
        Assert.Equal(2, g.GetCell(1, 0));
        Assert.Equal(4, g.GetCell(2, 0));
        Assert.Null(g.GetCell(3, 0));
        g.SetCell(2, 1, "last");
        Assert.Equal("last", g.GetCell(2, 1));
        Assert.Equal("n4", g.GetOriginal(2, 1));
    }

    [Fact]
    public void AcceptInsertStoresGeneratedKey()
    {
        var g = Make(2);
        var a = g.AddRow();
        g.SetCell(a, 1, "new");
        g.DeleteRow(0);
        g.AcceptChanges(new Dictionary<long, object?> { [a] = 42L });
        Assert.Equal(2, g.RowCount);
        Assert.Equal(42L, g.GetCell(1, 0));
        Assert.Equal("new", g.GetCell(1, 1));
        Assert.Equal(RowState.Unchanged, g.GetRowState(1));
        Assert.True(g.IsPersistedRow(1));
        Assert.Equal("new", g.GetOriginal(1, 1));
        g.SetCell(1, 1, "edited");
        Assert.Equal(RowState.Modified, g.GetRowState(1));
        g.DeleteRow(1);
        g.AcceptChanges(new Dictionary<long, object?>());
        Assert.Equal(1, g.RowCount);
    }

    [Fact]
    public void StreamingSourceBlocksEdits()
    {
        var g = Make(3, complete: false);
        Assert.False(g.CanEdit);
        Assert.Throws<InvalidOperationException>(() => g.SetCell(0, 1, "x"));
        Assert.Throws<InvalidOperationException>(() => g.AddRow());
        Assert.Throws<InvalidOperationException>(() => g.DeleteRow(0));
        Assert.Throws<InvalidOperationException>(() => g.RevertRow(0));
        Assert.Throws<InvalidOperationException>(g.DiscardAll);
        Assert.Throws<InvalidOperationException>(() => g.AcceptChanges(new Dictionary<long, object?>()));
    }

    [Fact]
    public void ReadOnlyColumnThrows()
    {
        var src = Source(3);
        var defs = Target().Columns.Take(2).ToArray();
        var target = Target() with { Columns = defs };
        var g = new EditableGridDataSource(src, target);
        Assert.Throws<ArgumentException>(() => g.SetCell(0, 2, new byte[1]));
    }

    [Fact]
    public void ChangedRaisedPerMutationAndForwarded()
    {
        var src = Source(3);
        var g = new EditableGridDataSource(src, Target());
        var n = 0;
        g.Changed += (_, _) => n++;
        g.SetCell(0, 1, "x");
        g.AddRow();
        g.DeleteRow(1);
        g.RevertRow(1);
        g.DiscardAll();
        g.AcceptChanges(new Dictionary<long, object?>());
        Assert.Equal(6, n);
        src.Raise();
        Assert.Equal(7, n);
    }

    [Fact]
    public void RowCountArithmetic()
    {
        var g = Make();
        g.AddRow();
        g.AddRow();
        g.DeleteRow(0);
        Assert.Equal(7, g.RowCount);
        g.AcceptChanges(new Dictionary<long, object?>());
        Assert.Equal(6, g.RowCount);
        Assert.Null(g.GetCell(6, 0));
        Assert.Null(g.GetCell(-1, 0));
    }

    [Fact]
    public void MillionRowsStayCheap()
    {
        var g = new EditableGridDataSource(new SyntheticGridSource(1_000_000, 3), Target());
        for (var i = 0; i < 10; i++)
        {
            g.SetCell(i * 100_000, 1, "e" + i);
        }

        Assert.Equal(10, g.PendingCount);
        Assert.Equal("e3", g.GetCell(300_000, 1));
        var inner = new SyntheticGridSource(1_000_000, 3);
        Assert.Equal(inner.GetCell(777, 2), g.GetCell(777, 2));
        _ = g.GetCell(778, 1);
        var before = GC.GetAllocatedBytesForCurrentThread();
        object? v = null;
        for (var i = 0; i < 1000; i++)
        {
            v = g.GetCell(500_001, 0);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(inner.GetCell(500_001, 0), v);
        Assert.True(allocated < 1000 * 40, $"allocated {allocated} bytes");
    }

    private sealed class Fake(int rows, bool complete) : IGridDataSource
    {
        private static readonly ColumnInfo[] Cols =
        [
            new("Id", "int", typeof(int), false, 0),
            new("Name", "text", typeof(string), true, 1),
            new("Data", "blob", typeof(byte[]), true, 2),
        ];

        public event EventHandler? Changed;

        public IReadOnlyList<ColumnInfo> Columns => Cols;

        public long RowCount => rows;

        public bool IsComplete => complete;

        public bool IsTruncated => false;

        public object? GetCell(long row, int column) => column switch
        {
            0 => (int)row,
            1 => "n" + row,
            _ => null,
        };

        public void Raise() => Changed?.Invoke(this, EventArgs.Empty);
    }
}
