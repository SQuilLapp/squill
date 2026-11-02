using Squill.Core;
using Squill.Core.Editing;
using Squill.Data;
using Squill.Presentation.Editing;
using Squill.Presentation.Results;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.Presentation.Tests.Editing;

public sealed class GridEditControllerTests
{
    private static readonly ColumnDefinition[] Defs =
    [
        new("Id", "int", false, true, null, true),
        new("Name", "text", false, false, null),
        new("Note", "text", true, false, null),
        new("Qty", "int", false, false, "0"),
    ];

    private static readonly ColumnInfo[] Cols =
    [
        new("Id", "int", typeof(int), false, 0),
        new("Name", "text", typeof(string), false, 1),
        new("Note", "text", typeof(string), true, 2),
        new("Qty", "int", typeof(int), false, 3),
        new("Computed", "int", typeof(int), true, 4),
    ];

    private static readonly EditTarget Target =
        new(null, "T", [.. Defs.Select((d, i) => new EditColumn(i, d))], ["Id"], new TableInfo(null, "T", TableKind.Table, Defs, [], []));

    private sealed class Fake(int rows, bool complete) : IGridDataSource
    {
        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public IReadOnlyList<ColumnInfo> Columns => Cols;
        public long RowCount => rows;
        public bool IsComplete => complete;
        public bool IsTruncated => false;

        public object? GetCell(long row, int column) => column switch
        {
            0 => (int)row,
            1 => "n" + row,
            2 => null,
            _ => 1,
        };
    }

    private sealed class Conn : IDatabaseConnection
    {
        public string ServerVersion => "1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(SchemaSnapshot.Empty);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Fixture
    {
        public Fixture(int rows = 4, bool complete = true)
        {
            Grid = new EditableGridDataSource(new Fake(rows, complete), Target);
            var session = new ConnectionSession(
                new ConnectionProfile(Guid.NewGuid(), "p", DatabaseDialect.Sqlite, "h", 1, "d", "u", null, null),
                new Conn(), SchemaSnapshot.Empty, null);
            Editor = new ResultEditorViewModel(Grid, Target, session, new FakeDialogService(), () => false);
            Controller = new GridEditController(Editor, Selection);
        }

        public EditableGridDataSource Grid { get; }
        public ResultEditorViewModel Editor { get; }
        public GridSelection Selection { get; } = new();
        public GridEditController Controller { get; }

        public Fixture At(long row, int column)
        {
            Selection.SelectCell(row, column, false);
            return this;
        }
    }

    [Fact]
    public void Begin_StartsWithCurrentTextOrTypedCharacter()
    {
        var f = new Fixture().At(1, 1);
        var raised = 0;
        f.Controller.EditStateChanged += (_, _) => raised++;

        Assert.True(f.Controller.Begin());
        Assert.True(f.Controller.IsEditing);
        Assert.Equal("n1", f.Controller.EditText);
        Assert.Equal((1L, 1), (f.Controller.EditRow, f.Controller.EditColumn));

        Assert.True(f.Controller.Begin("x"));
        Assert.Equal("x", f.Controller.EditText);
        Assert.True(raised >= 2);
    }

    [Fact]
    public void Begin_NullCellStartsEmpty()
    {
        var f = new Fixture().At(0, 2);

        Assert.True(f.Controller.Begin());
        Assert.Equal("", f.Controller.EditText);
    }

    [Fact]
    public void Begin_RefusedForReadOnlyColumnDeletedRowLoadingGridOrNoSelection()
    {
        var f = new Fixture();
        Assert.False(f.Controller.Begin());

        Assert.False(f.At(0, 4).Controller.Begin());

        f.Grid.DeleteRow(1);
        Assert.False(f.At(1, 1).Controller.Begin());

        var loading = new Fixture(complete: false).At(0, 1);
        Assert.False(loading.Controller.Begin());
        Assert.False(loading.Controller.IsEditing);
    }

    [Theory]
    [InlineData(EditMove.None, 1, 1)]
    [InlineData(EditMove.Down, 2, 1)]
    [InlineData(EditMove.Right, 1, 2)]
    [InlineData(EditMove.Left, 1, 0)]
    public void Commit_RecordsAndMoves(EditMove move, long row, int column)
    {
        var f = new Fixture().At(1, 1);
        f.Controller.Begin();
        f.Controller.SetText("zed");

        Assert.True(f.Controller.Commit(move));

        Assert.False(f.Controller.IsEditing);
        Assert.Equal("zed", f.Grid.GetCell(1, 1));
        Assert.Equal((row, column), (f.Selection.ActiveRow, f.Selection.ActiveColumn));
    }

    [Fact]
    public void Commit_ClampsAtTheEdges()
    {
        var f = new Fixture(rows: 2).At(1, 3);
        f.Controller.Begin();
        Assert.True(f.Controller.Commit(EditMove.Down));
        Assert.Equal(1, f.Selection.ActiveRow);

        f.Controller.Begin();
        Assert.True(f.Controller.Commit(EditMove.Right));
        Assert.Equal(4, f.Selection.ActiveColumn);

        f.At(0, 0);
        f.Controller.Begin();
        Assert.True(f.Controller.Commit(EditMove.Left));
        Assert.Equal(0, f.Selection.ActiveColumn);
    }

    [Fact]
    public void Commit_ErrorKeepsEditing()
    {
        var f = new Fixture().At(1, 3);
        f.Controller.Begin("abc");

        Assert.False(f.Controller.Commit(EditMove.Down));

        Assert.True(f.Controller.IsEditing);
        Assert.NotNull(f.Controller.Error);
        Assert.Equal(1, f.Selection.ActiveRow);
        Assert.False(f.Editor.HasPendingChanges);

        f.Controller.SetText("7");
        Assert.Null(f.Controller.Error);
        Assert.True(f.Controller.Commit(EditMove.None));
    }

    [Fact]
    public void Cancel_EndsWithoutChanges()
    {
        var f = new Fixture().At(1, 1);
        f.Controller.Begin("x");

        f.Controller.Cancel();

        Assert.False(f.Controller.IsEditing);
        Assert.False(f.Editor.HasPendingChanges);
    }

    [Fact]
    public void DataChange_CancelsEdit()
    {
        var f = new Fixture().At(1, 1);
        f.Controller.Begin();

        f.Grid.AddRow();

        Assert.False(f.Controller.IsEditing);
    }

    [Fact]
    public void ClearSelection_SetsNullOnEditableCellsOnly()
    {
        var f = new Fixture();
        f.Selection.SelectCell(0, 2, false);
        f.Selection.SelectCell(1, 4, true);
        f.Grid.DeleteRow(1);

        f.Controller.ClearSelection();

        Assert.Null(f.Grid.GetCell(0, 2));
        Assert.True(f.Grid.IsCellSet(0, 3));
        Assert.Null(f.Grid.GetCell(0, 3));
        Assert.Equal(RowState.Deleted, f.Grid.GetRowState(1));
        Assert.Equal(1, f.Grid.GetCell(1, 3));
    }

    [Fact]
    public void AddRow_SelectsFirstEditableCellWithoutEditing()
    {
        var f = new Fixture();

        var row = f.Controller.AddRow();

        Assert.Equal(4, row);
        Assert.Equal((4L, 0), (f.Selection.ActiveRow, f.Selection.ActiveColumn));
        Assert.False(f.Controller.IsEditing);
        Assert.Equal(RowState.Inserted, f.Controller.GetRowState(row));
    }

    [Fact]
    public void DeleteAndRevert_ApplyToEverySelectedRow()
    {
        var f = new Fixture();
        f.Selection.SelectCell(0, 1, false);
        f.Selection.SelectCell(2, 1, true);

        f.Controller.DeleteSelectedRows();

        Assert.Equal(RowState.Deleted, f.Controller.GetRowState(0));
        Assert.Equal(RowState.Deleted, f.Controller.GetRowState(1));
        Assert.Equal(RowState.Deleted, f.Controller.GetRowState(2));
        Assert.Equal(RowState.Unchanged, f.Controller.GetRowState(3));

        f.Controller.RevertSelectedRows();

        Assert.Equal(RowState.Unchanged, f.Controller.GetRowState(1));
        Assert.False(f.Editor.HasPendingChanges);
    }

    [Fact]
    public void ReadAccessors_ReflectEditorAndIssues()
    {
        var f = new Fixture().At(1, 1);

        Assert.True(f.Controller.IsColumnEditable(1));
        Assert.False(f.Controller.IsColumnEditable(4));
        Assert.False(f.Controller.IsColumnEditable(-1));
        Assert.False(f.Controller.IsCellModified(1, 1));

        f.Editor.TrySetCell(1, 1, "x");
        Assert.True(f.Controller.IsCellModified(1, 1));
        Assert.Equal(RowState.Modified, f.Controller.GetRowState(1));

        f.Editor.Issues.Add(new EditIssue(1, "Name", "bad"));
        f.Editor.Issues.Add(new EditIssue(2, null, "row bad"));
        Assert.True(f.Controller.IsCellInvalid(1, 1));
        Assert.False(f.Controller.IsCellInvalid(1, 2));
        Assert.True(f.Controller.IsCellInvalid(2, 3));
    }

    [Fact]
    public void Paste_WritesBlockFromActiveCell()
    {
        var f = new Fixture().At(1, 1);

        Assert.Null(f.Controller.Paste("a\t\\N\t5\r\nb\t\t6\r\n"));

        Assert.Equal("a", f.Grid.GetCell(1, 1));
        Assert.Null(f.Grid.GetCell(1, 2));
        Assert.Equal(5, f.Grid.GetCell(1, 3));
        Assert.Equal("b", f.Grid.GetCell(2, 1));
        Assert.Equal("", f.Grid.GetCell(2, 2));
        Assert.Equal(6, f.Grid.GetCell(2, 3));
    }

    [Fact]
    public void Paste_StopsAtGridEdgesAndSkipsReadOnlyColumns()
    {
        var f = new Fixture(rows: 2).At(1, 3);

        Assert.Null(f.Controller.Paste("9\tskipped\tignored\n8\t8"));

        Assert.Equal(9, f.Grid.GetCell(1, 3));
        Assert.Equal(2, f.Grid.RowCount);
        Assert.Equal(1, f.Editor.PendingCount);
    }

    [Fact]
    public void Paste_ReportsFirstErrorAndKeepsEarlierCells()
    {
        var f = new Fixture().At(0, 1);

        var error = f.Controller.Paste("ok\tn\tabc\nnext\tn\t1");

        Assert.StartsWith("Row 1, column Qty: ", error, StringComparison.Ordinal);
        Assert.Equal("ok", f.Grid.GetCell(0, 1));
        Assert.Equal("n", f.Grid.GetCell(0, 2));
        Assert.Equal("n1", f.Grid.GetCell(1, 1));
    }
}
