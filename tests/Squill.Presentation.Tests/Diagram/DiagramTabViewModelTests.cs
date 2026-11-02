using Squill.Core;
using Squill.Data;
using Squill.Platform.Storage;
using Squill.Presentation.Diagram;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.Presentation.Tests.Diagram;

public sealed class DiagramTabViewModelTests
{
    private static readonly ColumnDefinition Id = new("id", "int", false, true, null);
    private static readonly ColumnDefinition ParentId = new("p_id", "int", false, false, null);

    private static TableInfo Plain(string name) => new(null, name, TableKind.Table, [Id], [], []);

    private static TableInfo Child(string name, string parent) =>
        new(null, name, TableKind.Table, [Id, ParentId], [], [new ForeignKeyInfo("fk_" + name, ["p_id"], null, parent, ["id"])]);

    private static SchemaSnapshot Schema(params TableInfo[] t) => new(null, t);

    private sealed class FakeStore : IDiagramLayoutStore
    {
        public DiagramLayout? Saved { get; set; }
        public int Saves { get; private set; }
        public int Deletes { get; private set; }

        public ValueTask<DiagramLayout?> LoadAsync(Guid profileId, CancellationToken ct) => ValueTask.FromResult(Saved);

        public ValueTask SaveAsync(Guid profileId, DiagramLayout layout, CancellationToken ct)
        {
            Saved = layout;
            Saves++;
            return ValueTask.CompletedTask;
        }

        public ValueTask DeleteAsync(Guid profileId, CancellationToken ct)
        {
            Saved = null;
            Deletes++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeConnection(SchemaSnapshot next) : IDatabaseConnection
    {
        public string ServerVersion => "1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(next);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static (DiagramTabViewModel Vm, FakeStore Store) Create(SchemaSnapshot schema, SchemaSnapshot? refreshed = null, FakeStore? store = null)
    {
        store ??= new FakeStore();
        var session = new ConnectionSession(
            new ConnectionProfile(Guid.NewGuid(), "prod", DatabaseDialect.PostgreSql, "h", 1, "d", "u", null, null),
            new FakeConnection(refreshed ?? schema), schema, null);
        return (new DiagramTabViewModel(session, store, new FakeDialogService(), s => s.Length * 7, new ImmediateDispatcher()), store);
    }

    [Fact]
    public void Initial_LaysOutAndTitles()
    {
        var (vm, _) = Create(Schema(Plain("a"), Child("b", "a")));

        Assert.Equal("ER - prod", vm.Title);
        Assert.Equal(2, vm.Boxes.Count);
        Assert.Single(vm.Edges);
        Assert.Equal("", vm.StatusText);
    }

    [Fact]
    public async Task Initialize_RestoresSavedPositionsAndView()
    {
        var store = new FakeStore
        {
            Saved = new DiagramLayout(
                new Dictionary<string, DiagramPosition> { ["A"] = new(500, 600), ["gone"] = new(1, 1) }, 2, 10, 20),
        };
        var (vm, _) = Create(Schema(Plain("a"), Plain("b")), store: store);

        await vm.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new ErPoint(500, 600), new ErPoint(vm.Boxes["a"].X, vm.Boxes["a"].Y));
        Assert.False(vm.Boxes.ContainsKey("gone"));
        Assert.Equal((2.0, 10.0, 20.0), (vm.Zoom, vm.OffsetX, vm.OffsetY));
    }

    [Fact]
    public async Task Drag_MovesBoxReroutesAndSavesOnEnd()
    {
        var (vm, store) = Create(Schema(Plain("a"), Child("b", "a")));
        var before = vm.Edges[0].Points[1];
        var box = vm.Boxes["b"];

        vm.BeginDrag("b");
        vm.DragTo(new ErPoint(box.X + 5, box.Y + 5));
        vm.DragTo(new ErPoint(box.X + 305, box.Y + 105));

        Assert.Equal((box.X + 300, box.Y + 100), (vm.Boxes["b"].X, vm.Boxes["b"].Y));
        Assert.NotEqual(before, vm.Edges[0].Points[1]);
        Assert.Equal("b", vm.SelectedKey);
        Assert.Equal(0, store.Saves);

        vm.EndDrag();

        Assert.Equal(1, store.Saves);
        Assert.Equal(box.X + 300, store.Saved!.Positions["b"].X);
        Assert.Equal(2, store.Saved.Positions.Count);
        await vm.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.Equal(box.X + 300, vm.Boxes["b"].X);
    }

    [Fact]
    public void ZoomAt_KeepsWorldPointUnderAnchor()
    {
        var (vm, _) = Create(Schema(Plain("a")));
        vm.Pan(30, 40);
        var anchor = new ErPoint(200, 150);
        var world = vm.ToWorld(anchor);

        vm.ZoomAt(anchor, 2);

        Assert.Equal(2, vm.Zoom);
        var after = vm.ToScreen(world);
        Assert.Equal(anchor.X, after.X, 6);
        Assert.Equal(anchor.Y, after.Y, 6);
        vm.ZoomAt(anchor, 100);
        Assert.Equal(4, vm.Zoom);
        vm.Zoom = 0;
        Assert.Equal(0.1, vm.Zoom);
    }

    [Fact]
    public void FitToScreen_FitsDrawingInsideMargin()
    {
        var (vm, _) = Create(Schema(Plain("a"), Child("b", "a"), Child("c", "a")));

        vm.FitToScreenCommand.Execute(new ErSize(400, 300));

        var tl = vm.ToScreen(new ErPoint(vm.Boxes.Values.Min(b => b.X), vm.Boxes.Values.Min(b => b.Y)));
        var br = vm.ToScreen(new ErPoint(vm.Boxes.Values.Max(b => b.Right), vm.Boxes.Values.Max(b => b.Bottom)));
        Assert.True(tl.X >= 24 - 1e-6 && tl.Y >= 24 - 1e-6);
        Assert.True(br.X <= 376 + 1e-6 && br.Y <= 276 + 1e-6);
    }

    [Fact]
    public void Filter_HidesNonMatchingEntitiesIgnoringCase()
    {
        var (vm, _) = Create(Schema(Plain("users"), Plain("orders"), Plain("USER_ROLES")));

        vm.FilterText = "user";

        Assert.Equal(["USER_ROLES", "users"], vm.Model.Entities.Select(e => e.Key).Order(StringComparer.Ordinal));
        Assert.Equal(2, vm.Boxes.Count);
    }

    [Fact]
    public void Focus_ShowsNeighborhoodOfSelection()
    {
        var (vm, _) = Create(Schema(Plain("a"), Child("b", "a"), Child("c", "b"), Plain("z")));
        vm.SelectedKey = "a";

        vm.FocusSelectedCommand.Execute(null);
        Assert.Equal(["a", "b"], vm.Model.Entities.Select(e => e.Key));

        vm.FocusDepth = 2;
        Assert.Equal(["a", "b", "c"], vm.Model.Entities.Select(e => e.Key));

        vm.ShowAllCommand.Execute(null);
        Assert.Null(vm.FocusKey);
        Assert.Equal(4, vm.Model.Entities.Count);
    }

    [Fact]
    public void LargeSchema_IsCappedUntilShowAll()
    {
        var tables = new List<TableInfo> { Plain("hub") };
        tables.AddRange(Enumerable.Range(0, 250).Select(i => Plain($"t{i:000}")));
        tables.Add(Child("zz_child", "hub"));
        var (vm, _) = Create(Schema([.. tables]));

        Assert.Equal(50, vm.Model.Entities.Count);
        Assert.Contains(vm.Model.Entities, e => e.Key == "hub");
        Assert.Equal("Showing 50 of 252 tables, use the filter or Focus", vm.StatusText);

        vm.ShowAllCommand.Execute(null);

        Assert.Equal(252, vm.Model.Entities.Count);
        Assert.Equal("", vm.StatusText);
    }

    [Fact]
    public void HitTest_PicksTopmostBox()
    {
        var (vm, _) = Create(Schema(Plain("a"), Plain("b")));
        var b = vm.Boxes["b"];
        vm.BeginDrag("a");
        vm.DragTo(new ErPoint(vm.Boxes["a"].X, vm.Boxes["a"].Y));
        vm.DragTo(new ErPoint(b.X + 1, b.Y + 1));
        vm.EndDrag();

        var hit = vm.HitTestEntity(new ErPoint(b.X + 10, b.Y + 10));

        Assert.Equal("a", hit?.Key);
        Assert.Null(vm.HitTestEntity(new ErPoint(-1000, -1000)));
    }

    [Fact]
    public async Task Refresh_RebuildsFromNewSchema()
    {
        var (vm, _) = Create(Schema(Plain("a")), Schema(Plain("a"), Plain("b")));

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Boxes.Count);
    }

    [Fact]
    public async Task ResetLayout_DropsSavedPositionsAndDeletes()
    {
        var (vm, store) = Create(Schema(Plain("a"), Plain("b")));
        var original = vm.Boxes["a"];
        vm.BeginDrag("a");
        vm.DragTo(new ErPoint(0, 0));
        vm.DragTo(new ErPoint(900, 900));
        vm.EndDrag();

        await vm.ResetLayoutCommand.ExecuteAsync(null);

        Assert.Equal(original, vm.Boxes["a"]);
        Assert.Equal(1, store.Deletes);
    }

    [Fact]
    public void ActivateAndDesign_RaiseEventsWithTable()
    {
        var (vm, _) = Create(Schema(Plain("a")));
        List<string> events = [];
        vm.OpenTableRequested += (_, t) => events.Add("open " + t.Name);
        vm.DesignTableRequested += (_, t) => events.Add("design " + t.Name);

        vm.ActivateCommand.Execute("A");
        vm.DesignCommand.Execute("a");
        vm.ActivateCommand.Execute("missing");

        Assert.Equal(["open a", "design a"], events);
    }
}
