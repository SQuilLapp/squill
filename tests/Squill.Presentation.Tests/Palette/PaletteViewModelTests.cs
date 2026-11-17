using Squill.Presentation.Localization;
using Squill.Presentation.Palette;
using Xunit;

namespace Squill.Presentation.Tests.Palette;

public sealed class PaletteViewModelTests : IDisposable
{
    private readonly IDisposable _language = UiText.Override(UiLanguage.English);
    private readonly FakeDialogService _dialogs = new();
    private readonly PaletteActionRegistry _registry = new();
    private Func<string, CancellationToken, ValueTask<IReadOnlyList<PaletteItem>>> _objects = (_, _) => ValueTask.FromResult<IReadOnlyList<PaletteItem>>([]);
    private Func<string, CancellationToken, ValueTask<IReadOnlyList<PaletteItem>>> _history = (_, _) => ValueTask.FromResult<IReadOnlyList<PaletteItem>>([]);
    private bool _connected = true;
    private int _reloads;

    public void Dispose() => _language.Dispose();

    private static PaletteItem Item(PaletteItemKind kind, string title, bool enabled = true, Func<PaletteRunMode, CancellationToken, ValueTask>? run = null) =>
        new(kind, title, "", null, null, null, "x", [], enabled, 0, run ?? ((_, _) => ValueTask.CompletedTask));

    private static IReadOnlyList<PaletteItem> Many(PaletteItemKind kind, string prefix, int count) =>
        [.. Enumerable.Range(0, count).Select(i => Item(kind, prefix + i))];

    private static readonly string[] ActionKeys =
    [
        "palette.action.workspace.newTab", "palette.action.workspace.closeTab", "palette.action.query.format",
        "palette.action.query.explain", "palette.action.workspace.newTable", "palette.action.workspace.openDiagram",
    ];

    private void AddActions(int count = 6, Func<bool>? can = null, Func<CancellationToken, ValueTask>? run = null)
    {
        for (var i = 0; i < count; i++)
            _registry.Add(new PaletteAction($"a{i}", ActionKeys[i], "menu.query", null, "action", can ?? (() => true), run ?? (_ => ValueTask.CompletedTask)));
    }

    private PaletteViewModel Create() => new(
        _registry,
        (term, ct) => _objects(term, ct),
        (term, ct) => _history(term, ct),
        _ =>
        {
            _reloads++;
            return ValueTask.CompletedTask;
        },
        term => [Item(PaletteItemKind.Line, "Go to line " + term)],
        () => _connected,
        new ImmediateDispatcher(),
        _dialogs);

    private static async Task<PaletteViewModel> Typed(PaletteViewModel vm, string text)
    {
        vm.Text = text;
        await vm.Idle;
        return vm;
    }

    private static string[] Titles(PaletteViewModel vm, int section) => [.. vm.Sections[section].Items.Select(i => i.Title)];

    [Fact]
    public async Task All_WithTerm_OrdersSectionsAndAppliesLimits()
    {
        AddActions();
        _objects = (_, _) => ValueTask.FromResult(Many(PaletteItemKind.SchemaObject, "o", 7));
        _history = (_, _) => ValueTask.FromResult(Many(PaletteItemKind.History, "h", 4));
        var vm = Create();
        vm.Open();
        await vm.Idle;

        await Typed(vm, "e");

        Assert.Equal(["OBJECTS", "ACTIONS", "HISTORY"], vm.Sections.Select(s => s.Caption));
        Assert.Equal([5, 5, 3], vm.Sections.Select(s => s.Items.Count));
        Assert.StartsWith("17 results · ", vm.FooterText);
    }

    [Fact]
    public async Task All_EmptyText_ShowsEveryActionAndFiveHistoryEntries()
    {
        AddActions();
        _objects = (_, _) => throw new InvalidOperationException("objects must not be searched for an empty term");
        _history = (_, _) => ValueTask.FromResult(Many(PaletteItemKind.History, "h", 9));
        var vm = Create();

        vm.Open();
        await vm.Idle;

        Assert.Equal(["ACTIONS", "HISTORY"], vm.Sections.Select(s => s.Caption));
        Assert.Equal([6, 5], vm.Sections.Select(s => s.Items.Count));
    }

    [Fact]
    public async Task PrefixModes_ShowOneSectionEach()
    {
        AddActions();
        _objects = (_, _) => ValueTask.FromResult(Many(PaletteItemKind.SchemaObject, "o", 60));
        _history = (_, _) => ValueTask.FromResult(Many(PaletteItemKind.History, "h", 60));
        var vm = Create();
        vm.Open();

        await Typed(vm, ">");
        Assert.Equal((PaletteMode.Actions, "ACTIONS", 6), (vm.Mode, vm.Sections[0].Caption, vm.Sections[0].Items.Count));
        Assert.Single(vm.Sections);
        await Typed(vm, "#o");
        Assert.Equal((PaletteMode.Objects, "OBJECTS", 50), (vm.Mode, vm.Sections[0].Caption, vm.Sections[0].Items.Count));
        await Typed(vm, "!h");
        Assert.Equal((PaletteMode.History, "HISTORY", 50), (vm.Mode, vm.Sections[0].Caption, vm.Sections[0].Items.Count));
        await Typed(vm, ":12");
        Assert.Equal((PaletteMode.Line, "LINE", "Go to line 12"), (vm.Mode, vm.Sections[0].Caption, vm.Sections[0].Items[0].Title));
    }

    [Fact]
    public void SetMode_RewritesThePrefixAndKeepsTheTerm()
    {
        var vm = Create();
        vm.Text = "abc";

        vm.SetMode(PaletteMode.Actions);
        Assert.Equal(">abc", vm.Text);
        vm.SetMode(PaletteMode.Objects);
        Assert.Equal("#abc", vm.Text);
        vm.SetMode(PaletteMode.All);
        Assert.Equal("abc", vm.Text);
    }

    [Fact]
    public async Task MoveSelection_WrapsAndResetsAfterANewPublish()
    {
        AddActions(3);
        var vm = Create();
        vm.Open();
        await vm.Idle;

        vm.MoveSelection(-1);
        Assert.Equal(2, vm.SelectedIndex);
        vm.MoveSelection(1);
        Assert.Equal(0, vm.SelectedIndex);
        vm.MoveSelection(1);
        await Typed(vm, ">");

        Assert.Equal(0, vm.SelectedIndex);
    }

    [Fact]
    public async Task RunSelected_ClosesBeforeRunningTheItem()
    {
        bool? openWhenRun = null;
        var vm = Create();
        AddActions(1, run: _ =>
        {
            openWhenRun = vm.IsOpen;
            return ValueTask.CompletedTask;
        });
        vm.Open();
        await vm.Idle;

        await vm.RunSelectedAsync(PaletteRunMode.Default);

        Assert.False(openWhenRun);
        Assert.False(vm.IsOpen);
    }

    [Fact]
    public async Task RunSelected_CtrlEnter_PassesNewTab()
    {
        PaletteRunMode? seen = null;
        _objects = (_, _) => ValueTask.FromResult<IReadOnlyList<PaletteItem>>([Item(PaletteItemKind.SchemaObject, "orders", run: (m, _) =>
        {
            seen = m;
            return ValueTask.CompletedTask;
        })]);
        var vm = Create();
        vm.Open();
        await Typed(vm, "#or");

        await vm.RunSelectedAsync(PaletteRunMode.NewTab);

        Assert.Equal(PaletteRunMode.NewTab, seen);
    }

    [Fact]
    public async Task RunSelected_DisabledItem_DoesNothingAndStaysOpen()
    {
        var ran = false;
        AddActions(1, can: () => false, run: _ =>
        {
            ran = true;
            return ValueTask.CompletedTask;
        });
        var vm = Create();
        vm.Open();
        await vm.Idle;

        await vm.RunSelectedAsync(PaletteRunMode.Default);

        Assert.False(ran);
        Assert.True(vm.IsOpen);
    }

    [Fact]
    public async Task RunSelected_NoRows_DoesNothing()
    {
        var vm = Create();
        vm.Open();
        await Typed(vm, "zzz");

        await vm.RunSelectedAsync(PaletteRunMode.Default);

        Assert.True(vm.IsOpen);
        Assert.Null(vm.Selected);
    }

    [Fact]
    public async Task RunSelected_ThrowingItem_ShowsTheError()
    {
        AddActions(1, run: _ => throw new InvalidOperationException("boom"));
        var vm = Create();
        vm.Open();
        await vm.Idle;

        await vm.RunSelectedAsync(PaletteRunMode.Default);

        Assert.Equal([("The command failed", "boom")], _dialogs.Errors);
    }

    [Fact]
    public async Task Search_StaleSlowSearch_NeverOverwritesANewerOne()
    {
        var gate = new TaskCompletionSource();
        _objects = async (term, _) =>
        {
            if (term == "a")
            {
                await gate.Task;
                return [Item(PaletteItemKind.SchemaObject, "old")];
            }
            return [Item(PaletteItemKind.SchemaObject, "new")];
        };
        var vm = Create();
        vm.Open();
        await vm.Idle;
        vm.Text = "a";
        var first = vm.Idle;
        vm.Text = "ab";
        await vm.Idle;

        gate.SetResult();
        await first;

        Assert.Equal(["new"], Titles(vm, 0));
    }

    [Fact]
    public async Task Search_SlowObjects_ActionsAppearFirst()
    {
        AddActions();
        var gate = new TaskCompletionSource();
        _objects = async (_, _) =>
        {
            await gate.Task;
            return [Item(PaletteItemKind.SchemaObject, "late")];
        };
        var vm = Create();
        vm.Open();
        await vm.Idle;

        vm.Text = "e";
        Assert.Equal(["ACTIONS"], vm.Sections.Select(s => s.Caption));
        gate.SetResult();
        await vm.Idle;

        Assert.Equal(["OBJECTS", "ACTIONS"], vm.Sections.Select(s => s.Caption));
    }

    [Fact]
    public async Task Hint_EmptyObjectsMode_FollowsTheConnectionState()
    {
        var vm = Create();
        vm.Open();

        await Typed(vm, "#");
        Assert.Equal("Type the name of a table, view or routine", vm.Hint);
        _connected = false;
        await Typed(vm, "# ");
        await Typed(vm, "#");
        Assert.Equal("Connect to search objects", vm.Hint);
    }

    [Fact]
    public async Task Hint_NoMatch_SaysNoResults()
    {
        var vm = Create();
        vm.Open();

        await Typed(vm, "zzz");

        Assert.Equal("No results", vm.Hint);
        Assert.StartsWith("0 results · ", vm.FooterText);
    }

    [Fact]
    public async Task Footer_SingleResult_UsesTheSingular()
    {
        _objects = (_, _) => ValueTask.FromResult<IReadOnlyList<PaletteItem>>([Item(PaletteItemKind.SchemaObject, "orders")]);
        var vm = Create();
        vm.Open();

        await Typed(vm, "#or");

        Assert.Matches(@"^1 result · \d+ ms$", vm.FooterText);
    }

    [Fact]
    public async Task Open_ReloadsHistoryAndResetsTheState()
    {
        AddActions(2);
        var vm = Create();
        vm.Open();
        await Typed(vm, ">a");
        vm.Close();
        Assert.Empty(vm.Sections);
        Assert.Equal(-1, vm.SelectedIndex);

        vm.Open();
        await vm.Idle;

        Assert.Equal(2, _reloads);
        Assert.True(vm.IsOpen);
        Assert.Equal("", vm.Text);
        Assert.Equal(0, vm.SelectedIndex);
    }
}
