using Squill.Core;
using Squill.Data;
using Squill.Platform.Profiles;
using Squill.Platform.Settings;
using Squill.Platform.Ssh;
using Squill.Platform.Storage;
using Squill.Presentation.Connections;
using Squill.Presentation.Editor;
using Squill.Presentation.Localization;
using Squill.Presentation.Palette;
using Squill.Presentation.Sessions;
using Squill.Presentation.Workspace;
using Squill.Sql.Ddl;
using Xunit;

namespace Squill.Presentation.Tests.Palette;

/// <summary>The command palette driven through a real <see cref="WorkspaceViewModel"/> with two connected fake servers (production and staging, same table name in both).</summary>
public sealed class PaletteFlowTests : IDisposable
{
    private static readonly ColumnDefinition Id = new("id", "int", false, true, null);

    private static readonly DateTimeOffset Now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    private readonly IDisposable _language = UiText.Override(UiLanguage.English);
    private readonly FakeProvider _provider = new();
    private readonly List<StubSqlEditor> _editors = [];
    private readonly ConnectionProfile _prod = new(Guid.NewGuid(), "prod", DatabaseDialect.PostgreSql, "h1", 1, "ventes", "u", null, null) { Environment = ConnectionEnvironment.Production };
    private readonly ConnectionProfile _stage = new(Guid.NewGuid(), "stage", DatabaseDialect.PostgreSql, "h2", 1, "ventes", "u", null, null) { Environment = ConnectionEnvironment.Staging };
    private readonly WorkspaceViewModel _workspace;

    public PaletteFlowTests()
    {
        var registry = new ProviderRegistry();
        registry.Register(_provider);
        var store = new FakeStore([_prod, _stage]);
        var dialogs = new FakeDialogService();
        var history = new FakeHistory(
        [
            new HistoryEntry(Now.AddHours(-3), _prod.Id, "select * from commands", true, 3, TimeSpan.FromMilliseconds(12)),
            new HistoryEntry(Now.AddHours(-2), _stage.Id, "update commands set id = 1", true, 1, TimeSpan.FromMilliseconds(30)),
            new HistoryEntry(Now.AddMinutes(-5), _prod.Id, "select count(*) from orders_prod", true, 1, TimeSpan.FromMilliseconds(4)),
        ]);
        _workspace = new WorkspaceViewModel(
            new SessionManager(registry, store, new FakeTunnels()),
            new ConnectionListViewModel(store, registry, dialogs),
            history,
            new FakeSettings(),
            new ImmediateDispatcher(),
            dialogs,
            () =>
            {
                var editor = new StubSqlEditor();
                _editors.Add(editor);
                return editor;
            },
            store,
            time: new FixedClock());
    }

    public void Dispose()
    {
        _workspace.Dispose();
        _language.Dispose();
    }

    private PaletteViewModel Palette => _workspace.Palette;

    private async Task ConnectBothAsync()
    {
        foreach (var profile in new[] { _prod, _stage })
        {
            _workspace.ConnectionList.Profiles.Add(profile);
            await _workspace.ConnectCommand.ExecuteAsync(profile);
        }
    }

    private async Task TypeAsync(string text)
    {
        Palette.Text = text;
        await Palette.Idle;
    }

    private string[] Titles(int section) => [.. Palette.Sections[section].Items.Select(i => i.Title)];

    [Fact]
    public async Task All_Comm_ListsObjectsOfBothConnectionsThenActionsThenHistory()
    {
        await ConnectBothAsync();
        Palette.Open();
        await TypeAsync("comm");

        Assert.Equal(["OBJECTS", "ACTIONS", "HISTORY"], Palette.Sections.Select(s => s.Caption));
        var objects = Palette.Sections[0].Items;
        Assert.Equal(["commands", "commands"], objects.Select(o => o.Title));
        Assert.Equal(["prod", "stage"], objects.Select(o => o.Right).Order());
        Assert.Equal([ConnectionEnvironment.Production, ConnectionEnvironment.Staging], objects.Select(o => o.Environment!.Value).Order());
        Assert.Contains("Commit", Titles(1)[0], StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["update commands set id = 1", "select * from commands"], Titles(2)); // newest first
    }

    [Fact]
    public async Task Actions_ListsActionsOnly_AndCommitIsDisabledWithoutATransaction()
    {
        await ConnectBothAsync();
        Palette.Open();
        await TypeAsync(">");

        var section = Assert.Single(Palette.Sections);
        Assert.Equal("ACTIONS", section.Caption);
        var commit = section.Items.Single(i => i.Title.StartsWith("Commit", StringComparison.OrdinalIgnoreCase));
        Assert.False(commit.IsEnabled);
        Assert.Contains(section.Items, i => i.IsEnabled);
    }

    [Fact]
    public async Task Objects_FindsRoutinesAndTriggers()
    {
        await ConnectBothAsync();
        Palette.Open();

        await TypeAsync("#recalc");
        var routine = Assert.Single(Assert.Single(Palette.Sections).Items);
        Assert.Equal("recalc_totals", routine.Title);
        Assert.StartsWith("function", routine.Detail, StringComparison.OrdinalIgnoreCase);

        await TypeAsync("#trgaudit");
        var trigger = Assert.Single(Assert.Single(Palette.Sections).Items);
        Assert.Equal("trg_audit", trigger.Title);
        Assert.StartsWith("trigger", trigger.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task History_ListsNewestFirst_AndEnterAppendsWithoutRunningAnything()
    {
        await ConnectBothAsync();
        var editor = _editors[^1];
        editor.Text = "select 1";
        var executed = _provider.Executed.Count;
        Palette.Open();
        await TypeAsync("!");

        Assert.Equal(["select count(*) from orders_prod", "update commands set id = 1", "select * from commands"], Titles(0));

        await Palette.RunSelectedAsync(PaletteRunMode.Default);

        Assert.False(Palette.IsOpen);
        Assert.EndsWith("select count(*) from orders_prod", editor.Text, StringComparison.Ordinal);
        Assert.StartsWith("select 1", editor.Text, StringComparison.Ordinal);
        Assert.Equal(executed, _provider.Executed.Count); // inserted, never executed
    }

    [Fact]
    public async Task Line_ColonThree_MovesTheCaret()
    {
        await ConnectBothAsync();
        var editor = _editors[^1];
        editor.Text = "one\ntwo\nthree\nfour";
        Palette.Open();
        await TypeAsync(":3");

        await Palette.RunSelectedAsync(PaletteRunMode.Default);

        Assert.Equal(8, editor.CaretOffset); // the start of "three"
    }

    [Fact]
    public async Task Enter_OnATableOfTheOtherConnection_ActivatesItAndOpensTheTab()
    {
        await ConnectBothAsync();
        _workspace.ActiveSession = _workspace.Sessions.Single(s => s.Profile.Id == _stage.Id);
        var tabs = _workspace.Tabs.Count;
        Palette.Open();
        await TypeAsync("#orders_prod");

        await Palette.RunSelectedAsync(PaletteRunMode.Default);

        Assert.Equal(_prod.Id, _workspace.ActiveSession!.Profile.Id);
        Assert.Equal(tabs + 1, _workspace.Tabs.Count);
        Assert.Equal(_prod.Id, _workspace.ActiveTab!.Session.Profile.Id);
    }

    [Fact]
    public async Task CtrlEnter_OnATable_OpensAQueryTabWithTheQuotedSelect()
    {
        await ConnectBothAsync();
        var tabs = _workspace.Tabs.Count;
        Palette.Open();
        await TypeAsync("#orders_prod");

        await Palette.RunSelectedAsync(PaletteRunMode.NewTab);

        Assert.Equal(tabs + 1, _workspace.Tabs.Count);
        Assert.Equal("SELECT * FROM " + SqlQuoting.QualifiedName("public", "orders_prod", DatabaseDialect.PostgreSql), _editors[^1].Text);
    }

    [Fact]
    public async Task CloseAndReopen_ResetsTheText()
    {
        await ConnectBothAsync();
        Palette.Open();
        await TypeAsync(">comm");
        Palette.Close();

        Palette.Open();
        await Palette.Idle;

        Assert.Equal("", Palette.Text);
        Assert.Equal(PaletteMode.All, Palette.Mode);
        Assert.Equal(["ACTIONS", "HISTORY"], Palette.Sections.Select(s => s.Caption));
    }

    [Fact]
    public async Task ALanguageSwitch_ChangesTheTitlesAndTheSections()
    {
        await ConnectBothAsync();
        Palette.Open();
        await Palette.Idle;
        var english = (Palette.Sections.Select(s => s.Caption).ToArray(), Titles(0));
        Palette.Close();

        using var _ = UiText.Override(UiLanguage.French);
        Palette.Open();
        await Palette.Idle;

        Assert.NotEqual(english.Item1, Palette.Sections.Select(s => s.Caption));
        Assert.NotEqual(english.Item2, Titles(0));
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new NoTimer();
    }

    private sealed class NoTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeStore(IReadOnlyList<ConnectionProfile> profiles) : IProfileStore
    {
        public ValueTask<IReadOnlyList<ConnectionProfile>> ListAsync(CancellationToken ct) => ValueTask.FromResult(profiles);

        public ValueTask SaveAsync(ConnectionProfile profile, string? password, string? sshSecret, CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask DeleteAsync(Guid id, CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask<string?> GetPasswordAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>(null);

        public ValueTask<string?> GetSshSecretAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>(null);
    }

    private sealed class FakeTunnels : ISshTunnelFactory
    {
        public ValueTask<ISshTunnel> OpenAsync(SshTunnelSettings settings, string? secret, string remoteHost, int remotePort, SshHostKeyPrompt? prompt, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class FakeHistory(IReadOnlyList<HistoryEntry> entries) : IQueryHistoryStore
    {
        public ValueTask AppendAsync(HistoryEntry e, CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<HistoryEntry>> ListAsync(int max, CancellationToken ct) => ValueTask.FromResult(entries);

        public ValueTask ClearAsync(CancellationToken ct) => ValueTask.CompletedTask;
    }

    private sealed class FakeSettings : ISettingsStore
    {
        public ValueTask<AppSettings> LoadAsync(CancellationToken ct) => ValueTask.FromResult(AppSettings.Default);

        public ValueTask SaveAsync(AppSettings settings, CancellationToken ct) => ValueTask.CompletedTask;
    }

    private sealed class FakeProvider : IDatabaseProvider
    {
        public DatabaseDialect Dialect => DatabaseDialect.PostgreSql;

        public List<string> Executed { get; } = [];

        public ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile profile, string? password, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IDatabaseConnection>(new FakeConnection(this, profile.Name));
    }

    // Both servers hold "commands"; "orders_prod" lives on prod only; the routine and the trigger on stage only.
    private sealed class FakeConnection(FakeProvider owner, string name) : IDatabaseConnection
    {
        public string ServerVersion => "1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
        {
            owner.Executed.Add(sql);
            return ValueTask.FromResult<IResultReader>(new EmptyReader());
        }

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken)
        {
            var commands = new TableInfo("public", "commands", TableKind.Table, [Id], [], []);
            if (name == "prod")
                return ValueTask.FromResult(new SchemaSnapshot("public", [commands, new TableInfo("public", "orders_prod", TableKind.Table, [Id], [], [])]));
            return ValueTask.FromResult(new SchemaSnapshot("public", [commands])
            {
                Routines = [new RoutineInfo("public", "recalc_totals", RoutineKind.Function, "recalc_totals()", "integer", "select 1")],
                Triggers = [new TriggerInfo("public", "commands", "trg_audit", "AFTER", "INSERT", null)],
            });
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class EmptyReader : IResultReader
    {
        public IReadOnlyList<ColumnInfo> Columns { get; } = [];

        public long RecordsAffected => -1;

        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken) => ValueTask.FromResult<object?[][]?>(null);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
