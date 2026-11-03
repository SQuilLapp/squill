using Squill.Core;
using Squill.Core.Editing;
using Squill.Presentation.Editing;
using Squill.Presentation.Results;
using Xunit;

namespace Squill.Presentation.Tests.Editing;

public sealed class ChangeSetBuilderTests
{
    private static readonly ColumnDefinition[] Defs =
    [
        new("Id", "int", false, true, null, true),
        new("Name", "text", false, false, null),
        new("Note", "text", true, false, null),
        new("Qty", "int", false, false, "0"),
    ];

    private static EditTarget Target() =>
        new("main", "T", [.. Defs.Select((d, i) => new EditColumn(i, d))], ["Id"],
            new TableInfo("main", "T", TableKind.Table, Defs, [], []));

    private static EditableGridDataSource Make() => new(new Fake(5), Target());

    [Fact]
    public void NoChangesGivesEmptyLists()
    {
        var set = ChangeSetBuilder.Build(Make(), Target());
        Assert.Empty(set.Edits);
        Assert.Empty(set.Issues);
    }

    [Fact]
    public void OrderIsDeletesUpdatesInserts()
    {
        var g = Make();
        var added = g.AddRow();
        g.SetCell(added, 1, "new");
        g.SetCell(3, 2, "u3");
        g.SetCell(1, 2, "u1");
        g.DeleteRow(4);
        g.DeleteRow(0);
        var set = ChangeSetBuilder.Build(g, Target());
        Assert.Equal([0L, 4L, 1L, 3L, added], set.Edits.Select(e => e.Row));
        Assert.IsType<DeleteRow>(set.Edits[0].Edit);
        Assert.IsType<UpdateRow>(set.Edits[2].Edit);
        Assert.IsType<InsertRow>(set.Edits[4].Edit);
    }

    [Fact]
    public void UpdateMergesCellsAndKeepsOriginalKey()
    {
        var g = Make();
        g.SetCell(2, 0, 99);
        g.SetCell(2, 2, null);
        g.SetCell(2, 1, "x");
        var u = Assert.IsType<UpdateRow>(Assert.Single(ChangeSetBuilder.Build(g, Target()).Edits).Edit);
        Assert.Equal([new ColumnValue("Id", 2)], u.Key);
        Assert.Equal([new ColumnValue("Id", 99), new ColumnValue("Name", "x"), new ColumnValue("Note", null)], u.Changes);
    }

    [Fact]
    public void DeleteUsesOriginalKey()
    {
        var g = Make();
        g.DeleteRow(3);
        var d = Assert.IsType<DeleteRow>(Assert.Single(ChangeSetBuilder.Build(g, Target()).Edits).Edit);
        Assert.Equal([new ColumnValue("Id", 3)], d.Key);
    }

    [Fact]
    public void InsertOmitsUnsetColumns()
    {
        var g = Make();
        var r = g.AddRow();
        g.SetCell(r, 1, "n");
        var set = ChangeSetBuilder.Build(g, Target());
        Assert.Empty(set.Issues);
        Assert.Equal([new ColumnValue("Name", "n")], Assert.IsType<InsertRow>(set.Edits[0].Edit).Values);
    }

    [Fact]
    public void RowEditedBackYieldsNoEdit()
    {
        var g = Make();
        g.SetCell(1, 1, "x");
        g.SetCell(1, 1, "n1");
        Assert.Empty(ChangeSetBuilder.Build(g, Target()).Edits);
    }

    [Fact]
    public void NullInNonNullableColumnIsReported()
    {
        var g = Make();
        g.SetCell(1, 1, null);
        var r = g.AddRow();
        g.SetCell(r, 1, null);
        var issues = ChangeSetBuilder.Build(g, Target()).Issues;
        Assert.Equal([(1, "Name", "Name cannot be NULL", "edit.columnNull", "Name"), (r, "Name", "Name cannot be NULL", "edit.columnNull", "Name")], Describe(issues));
    }

    [Fact]
    public void RequiredColumnOfInsertIsReported()
    {
        var g = Make();
        var r = g.AddRow();
        var issues = ChangeSetBuilder.Build(g, Target()).Issues;
        Assert.Equal([(r, "Name", "Name is required", "edit.columnRequired", "Name")], Describe(issues));
    }

    [Fact]
    public void DuplicateKeyInPendingInsertsIsReportedOnLaterRow()
    {
        var g = Make();
        var a = g.AddRow();
        var b = g.AddRow();
        foreach (var r in new[] { a, b })
        {
            g.SetCell(r, 0, 7);
            g.SetCell(r, 1, "n");
        }

        var issues = ChangeSetBuilder.Build(g, Target()).Issues;
        Assert.Equal([(b, null, "duplicate key in pending rows", "edit.duplicateKey", "")], Describe(issues));
    }

    [Fact]
    public void SeveralIssuesAreCollected()
    {
        var g = Make();
        g.SetCell(0, 1, null);
        g.AddRow();
        g.AddRow();
        Assert.Equal(3, ChangeSetBuilder.Build(g, Target()).Issues.Count);
    }

    private sealed class Fake(int rows) : IGridDataSource
    {
        private static readonly ColumnInfo[] Cols =
        [
            new("Id", "int", typeof(int), false, 0),
            new("Name", "text", typeof(string), false, 1),
            new("Note", "text", typeof(string), true, 2),
            new("Qty", "int", typeof(int), false, 3),
        ];

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public IReadOnlyList<ColumnInfo> Columns => Cols;

        public long RowCount => rows;

        public bool IsComplete => true;

        public bool IsTruncated => false;

        public object? GetCell(long row, int column) => column switch
        {
            0 => (int)row,
            1 => "n" + row,
            2 => "t" + row,
            _ => 1,
        };
    }

    private static (long Row, string? Column, string Message, string? Code, string Args)[] Describe(IEnumerable<EditIssue> issues) =>
        [.. issues.Select(i => (i.Row, i.Column, i.Message, i.Code, string.Join(",", i.Args ?? [])))];
}
