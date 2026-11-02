using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Squill.App.Avalonia.Controls;
using Squill.Core;
using Squill.Core.Editing;
using Squill.Data;
using Squill.Presentation.Editing;
using Squill.Presentation.Query;
using Squill.Presentation.Results;
using Squill.Presentation.Sessions;
using Xunit;
using ColumnDefinition = Squill.Core.ColumnDefinition;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class GridEditingUiTests
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

    private sealed class Source : IGridDataSource
    {
        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public IReadOnlyList<ColumnInfo> Columns => Cols;
        public long RowCount => 4;
        public bool IsComplete => true;
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

    private sealed class Fixture : IDisposable
    {
        public Fixture(bool editable = true)
        {
            var grid = new EditableGridDataSource(new Source(), Target);
            var session = new ConnectionSession(
                new ConnectionProfile(Guid.NewGuid(), "p", DatabaseDialect.Sqlite, "h", 1, "d", "u", null, null),
                new Conn(), SchemaSnapshot.Empty, null);
            Editor = new ResultEditorViewModel(grid, Target, session, new Dialogs(), () => false);
            Selection = new GridSelection();
            Controller = new GridEditController(Editor, Selection);
            View = new DataGridView { Selection = Selection, DataSource = Editor.Grid };
            if (editable)
            {
                View.EditController = Controller;
            }

            Window = new Window { Width = 600, Height = 300, Content = View };
            Window.Show();
            View.Canvas.Focus();
        }

        public void Dispose() => Window.Close();

        public ResultEditorViewModel Editor { get; }
        public GridSelection Selection { get; }
        public GridEditController Controller { get; }
        public DataGridView View { get; }
        public Window Window { get; }

        public Fixture At(long row, int column)
        {
            Selection.SelectCell(row, column, false);
            View.Canvas.InvalidateVisual();
            return this;
        }
    }

    private static Task RunAsync(Action body) => Headless.Session.Dispatch(body, TestContext.Current.CancellationToken);

    [Fact]
    public Task F2_ShowsTheTextBoxOverTheActiveCell() => RunAsync(() =>
    {
        using var f = new Fixture().At(1, 1);
        f.Window.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
        Assert.True(f.Controller.IsEditing);
        Assert.True(f.View.CellEditor.IsVisible);
        Assert.Equal("n1", f.View.CellEditor.Text);
        var rect = f.View.Canvas.CellRect(1, 1)!.Value;
        Assert.Equal(rect.X, f.View.CellEditor.Margin.Left);
        Assert.Equal(rect.Y, f.View.CellEditor.Margin.Top);
        Assert.Equal(rect.Width, f.View.CellEditor.Width);
    });

    [Fact]
    public Task TypingThenEnter_CommitsAndMovesDown() => RunAsync(() =>
    {
        using var f = new Fixture().At(1, 1);
        f.Window.KeyTextInput("x");
        Assert.True(f.Controller.IsEditing);
        Assert.Equal("x", f.View.CellEditor.Text);
        f.View.CellEditor.Text = "xyz";
        f.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.False(f.Controller.IsEditing);
        Assert.False(f.View.CellEditor.IsVisible);
        Assert.Equal("xyz", f.Editor.Grid.GetCell(1, 1));
        Assert.Equal(2, f.Selection.ActiveRow);
    });

    [Fact]
    public Task Escape_CancelsTheEdit() => RunAsync(() =>
    {
        using var f = new Fixture().At(1, 1);
        f.Window.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
        f.View.CellEditor.Text = "changed";
        f.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Assert.False(f.Controller.IsEditing);
        Assert.False(f.View.CellEditor.IsVisible);
        Assert.Equal("n1", f.Editor.Grid.GetCell(1, 1));
    });

    [Fact]
    public Task InvalidValue_KeepsTheBoxOpenWithTheError() => RunAsync(() =>
    {
        using var f = new Fixture().At(1, 3);
        f.Window.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
        f.View.CellEditor.Text = "not a number";
        f.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.True(f.Controller.IsEditing);
        Assert.True(f.View.CellEditor.IsVisible);
        Assert.NotNull(f.Controller.Error);
        Assert.Equal(f.Controller.Error, ToolTip.GetTip(f.View.CellEditor));
    });

    [Fact]
    public Task Delete_SetsNullOnEditableCells() => RunAsync(() =>
    {
        using var f = new Fixture().At(1, 2);
        f.Editor.TrySetCell(1, 2, "text");
        f.Window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        Assert.Null(f.Editor.Grid.GetCell(1, 2));
        Assert.False(f.Controller.IsEditing);
    });

    [Fact]
    public Task ReadOnlyColumn_DoesNotStartAnEdit() => RunAsync(() =>
    {
        using var f = new Fixture().At(1, 4);
        f.Window.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
        f.Window.KeyTextInput("a");
        Assert.False(f.Controller.IsEditing);
        Assert.False(f.View.CellEditor.IsVisible);
    });

    [Fact]
    public Task NoController_MeansNoEditing() => RunAsync(() =>
    {
        using var f = new Fixture(editable: false).At(1, 1);
        f.Window.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);
        f.Window.KeyTextInput("a");
        f.Window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        Assert.False(f.Controller.IsEditing);
        Assert.False(f.View.CellEditor.IsVisible);
        Assert.Equal("n1", f.Editor.Grid.GetCell(1, 1));
        Assert.Equal(RowState.Unchanged, f.Editor.Grid.GetRowState(1));
    });

    [Fact]
    public Task Rendering_DoesNotThrowForEachRowState() => RunAsync(() =>
    {
        using var f = new Fixture();
        f.Editor.TrySetCell(0, 1, "edited");
        f.Editor.DeleteRows([1]);
        f.Editor.AddRow();
        var states = Enumerable.Range(0, 5).Select(r => f.Controller.GetRowState(r)).ToArray();
        Assert.Contains(RowState.Modified, states);
        Assert.Contains(RowState.Deleted, states);
        Assert.Contains(RowState.Inserted, states);
        var bitmap = new RenderTargetBitmap(new PixelSize(600, 300));
        bitmap.Render(f.View);
    });
}
