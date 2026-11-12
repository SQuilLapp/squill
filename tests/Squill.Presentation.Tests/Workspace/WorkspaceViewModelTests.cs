using System.Globalization;
using Squill.Presentation.Localization;
using Squill.Core;
using Squill.Data;
using Squill.Platform;
using Squill.Platform.Profiles;
using Squill.Platform.Settings;
using Squill.Platform.Ssh;
using Squill.Platform.Storage;
using Squill.Presentation.Connections;
using Squill.Presentation.Editor;
using Squill.Presentation.Sessions;
using Squill.Presentation.Safety;
using Squill.Core.Transactions;
using Squill.Sql.Transactions;
using Squill.Presentation.Workspace;
using Xunit;

namespace Squill.Presentation.Tests.Workspace;

public sealed class WorkspaceViewModelTests : IDisposable
{
    private static readonly TableInfo Users = new(null, "users", TableKind.Table, [], [], []);

    private static readonly TableInfo GridTable = new(null, "g", TableKind.Table, [new ColumnDefinition("id", "int", false, true, null)], [], []);

    private readonly FakeDialogService _dialogs = new();
    private readonly FakeProvider _provider = new();
    private readonly List<StubSqlEditor> _editors = [];
    private readonly WorkspaceViewModel _workspace;
    private readonly TickClock _time = new();

    public WorkspaceViewModelTests()
    {
        var registry = new ProviderRegistry();
        registry.Register(_provider);
        var store = new FakeStore();
        _workspace = new WorkspaceViewModel(
            new SessionManager(registry, store, new FakeTunnels()),
            new ConnectionListViewModel(store, registry, _dialogs),
            new FakeHistory(),
            new FakeSettings(),
            new ImmediateDispatcher(),
            _dialogs,
            () =>
            {
                var editor = new StubSqlEditor();
                _editors.Add(editor);
                return editor;
            },
            store,
            time: _time);
    }

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public async Task ConfirmClose_WithoutUnsavedDesigner_DoesNotAsk()
    {
        Assert.True(await _workspace.ConfirmCloseAsync());
        Assert.Empty(_dialogs.Confirms);
    }

    [Fact]
    public async Task ClosingTheQueryTab_ClosesItsPlanTab_AndOpenPlanIsIdempotent()
    {
        await ConnectAsync();
        var tab = _workspace.Tabs[0];
        var plan = new Squill.Core.Plans.QueryPlan(
            DatabaseDialect.PostgreSql, Squill.Core.Plans.PlanMode.Estimated, "select 1",
            new Squill.Core.Plans.PlanNode(0, "Result", Squill.Core.Plans.PlanNodeKind.Result, null, null, null, null, null, null, null, null, null, [], [], false, []),
            [], [], null, null, null, false, Squill.Core.Plans.PlanSourceFormat.Text, "Result");
        var planTab = new Squill.Presentation.Plans.PlanTabViewModel(tab, Squill.Presentation.Plans.PlanViewModel.Create(plan), "select 1", [], Squill.Core.Plans.PlanMode.Estimated, "");

        _workspace.OpenPlan(planTab);
        _workspace.OpenPlan(planTab);
        Assert.Single(_workspace.Plans);
        Assert.Same(planTab, _workspace.ActivePlan);

        planTab.CloseCommand.Execute(null);
        Assert.Empty(_workspace.Plans);
        Assert.Null(_workspace.ActivePlan);

        _workspace.OpenPlan(planTab);
        await _workspace.CloseTabCommand.ExecuteAsync(tab);
        Assert.Empty(_workspace.Plans);
    }

    private sealed class TickClock : TimeProvider
    {
        public TimerCallback? Callback { get; private set; }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Callback = callback;
            return new NoTimer();
        }
    }

    private sealed class NoTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static void Open(ConnectionSession session, int statements)
    {
        var tx = session.Transaction;
        tx.ObserveSuccess("BEGIN", StatementClassifier.Classify("BEGIN", DatabaseDialect.PostgreSql), -1, "Q");
        for (var i = 0; i < statements; i++)
            tx.ObserveSuccess("INSERT INTO t VALUES (1)", StatementClassifier.Classify("INSERT INTO t VALUES (1)", DatabaseDialect.PostgreSql), 1, "Q");
    }

    [Fact]
    public async Task Closing_the_last_tab_with_an_open_transaction_asks_and_Back_keeps_the_tab()
    {
        await ConnectAsync();
        var session = _workspace.Sessions[0];
        Open(session, 2);
        _dialogs.TransactionChoices.Enqueue(TransactionChoice.Cancel);

        await _workspace.CloseTabCommand.ExecuteAsync(_workspace.Tabs[0]);

        var request = Assert.Single(_dialogs.TransactionRequests);
        Assert.Equal(2, request.PendingCount);
        Assert.Equal(UiText.Get("tx.ask.closeTab"), request.Title);
        Assert.Single(_workspace.Tabs);
        Assert.Equal(TransactionState.Open, session.Transaction.State);
    }

    [Fact]
    public async Task Rollback_then_closes_and_Commit_then_closes()
    {
        await ConnectAsync();
        var session = _workspace.Sessions[0];
        Open(session, 1);
        _dialogs.TransactionChoices.Enqueue(TransactionChoice.Rollback);
        await _workspace.CloseTabCommand.ExecuteAsync(_workspace.Tabs[0]);
        Assert.Empty(_workspace.Tabs);
        Assert.Equal(TransactionState.None, session.Transaction.State);
        Assert.Contains("ROLLBACK", _provider.Executed);

        _workspace.NewTabCommand.Execute(null);
        Open(session, 1);
        _dialogs.TransactionChoices.Enqueue(TransactionChoice.Commit);
        await _workspace.CloseTabCommand.ExecuteAsync(_workspace.Tabs[0]);
        Assert.Empty(_workspace.Tabs);
        Assert.Contains("COMMIT", _provider.Executed);
    }

    [Fact]
    public async Task A_failing_commit_keeps_the_tab_and_shows_the_error()
    {
        await ConnectAsync();
        var session = _workspace.Sessions[0];
        Open(session, 1);
        _provider.FailOn = "COMMIT";
        _dialogs.TransactionChoices.Enqueue(TransactionChoice.Commit);

        await _workspace.CloseTabCommand.ExecuteAsync(_workspace.Tabs[0]);

        Assert.Single(_workspace.Tabs);
        Assert.Single(_dialogs.Errors);
        Assert.Equal(TransactionState.Open, session.Transaction.State);
    }

    [Fact]
    public async Task Closing_a_tab_while_another_tab_of_the_session_remains_asks_nothing()
    {
        await ConnectAsync();
        var session = _workspace.Sessions[0];
        _workspace.NewTabCommand.Execute(null);
        Open(session, 1);

        await _workspace.CloseTabCommand.ExecuteAsync(_workspace.Tabs[0]);

        Assert.Empty(_dialogs.TransactionRequests);
        Assert.Single(_workspace.Tabs);
    }

    [Fact]
    public async Task Disconnect_asks_and_Back_aborts_before_anything_is_removed()
    {
        await ConnectAsync();
        Open(_workspace.Sessions[0], 1);
        _dialogs.TransactionChoices.Enqueue(TransactionChoice.Cancel);

        await _workspace.DisconnectCommand.ExecuteAsync(null);

        Assert.Equal(UiText.Get("tx.ask.disconnect"), Assert.Single(_dialogs.TransactionRequests).Title);
        Assert.Single(_workspace.Sessions);
        Assert.Single(_workspace.Tabs);

        _dialogs.TransactionChoices.Enqueue(TransactionChoice.Rollback);
        await _workspace.DisconnectCommand.ExecuteAsync(null);
        Assert.Empty(_workspace.Sessions);
    }

    [Fact]
    public async Task PrepareExit_returns_true_without_asking_when_nothing_is_open()
    {
        await ConnectAsync();
        Assert.False(_workspace.HasOpenTransaction);
        Assert.True(await _workspace.PrepareExitAsync(CancellationToken.None));
        Assert.Empty(_dialogs.TransactionRequests);
    }

    [Fact]
    public async Task PrepareExit_asks_per_session_and_stops_at_the_first_Back()
    {
        await ConnectAsync();
        await ConnectAsync();
        Assert.Equal(2, _workspace.Sessions.Count);
        foreach (var s in _workspace.Sessions)
            Open(s, 1);
        Assert.True(_workspace.HasOpenTransaction);

        _dialogs.TransactionChoices.Enqueue(TransactionChoice.Rollback);
        _dialogs.TransactionChoices.Enqueue(TransactionChoice.Cancel);
        Assert.False(await _workspace.PrepareExitAsync(CancellationToken.None));
        Assert.Equal(2, _dialogs.TransactionRequests.Count);
        Assert.All(_dialogs.TransactionRequests, r => Assert.Equal(UiText.Get("tx.ask.exit"), r.Title));

        _dialogs.TransactionChoices.Enqueue(TransactionChoice.Rollback);
        Assert.True(await _workspace.PrepareExitAsync(CancellationToken.None));
        Assert.False(_workspace.HasOpenTransaction);
    }

    [Fact]
    public async Task A_failed_transaction_request_carries_the_failed_state()
    {
        await ConnectAsync();
        var session = _workspace.Sessions[0];
        Open(session, 1);
        await session.Transaction.ObserveFailureAsync(new InvalidOperationException("x"), StatementClassifier.Classify("DELETE FROM t", DatabaseDialect.PostgreSql), CancellationToken.None);
        _dialogs.TransactionChoices.Enqueue(TransactionChoice.Cancel);

        await _workspace.PrepareExitAsync(CancellationToken.None);

        var request = Assert.Single(_dialogs.TransactionRequests);
        Assert.Equal(TransactionState.Failed, request.State);
        Assert.Equal(UiText.Get("tx.ask.failed"), request.Message);
    }

    [Fact]
    public async Task The_timer_refreshes_the_elapsed_text_of_open_transactions()
    {
        await ConnectAsync();
        var session = _workspace.Sessions[0];
        Open(session, 1);
        var raised = new List<string?>();
        session.Transaction.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _time.Callback!(null);

        Assert.Contains(nameof(SessionTransaction.ElapsedText), raised);
    }

    private static ConnectionProfile Profile() =>
        new(Guid.NewGuid(), "p", DatabaseDialect.PostgreSql, "h", 1, "d", "u", null, null);

    private async Task ConnectAsync()
    {
        var profile = Profile();
        _workspace.ConnectionList.Profiles.Add(profile);
        await _workspace.ConnectCommand.ExecuteAsync(profile);
    }

    [Fact]
    public async Task Connect_WhileOpening_MarksTheNodeAsConnectingUntilDone()
    {
        _provider.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var profile = Profile();
        _workspace.ConnectionList.Profiles.Add(profile);

        var connecting = _workspace.ConnectCommand.ExecuteAsync(profile);
        var node = Assert.Single(_workspace.Explorer.Roots);
        Assert.True(node.IsConnecting);

        _provider.Gate.SetResult();
        await connecting;
        Assert.False(node.IsConnecting);
    }

    [Fact]
    public async Task Connect_Fails_ClearsTheConnectingFlag()
    {
        _provider.Failure = new InvalidOperationException("down");
        var profile = Profile();
        _workspace.ConnectionList.Profiles.Add(profile);

        await _workspace.ConnectCommand.ExecuteAsync(profile);

        Assert.False(Assert.Single(_workspace.Explorer.Roots).IsConnecting);
    }

    [Fact]
    public async Task Connect_Succeeds_CreatesExplorerAndFirstTab()
    {
        await ConnectAsync();

        Assert.NotNull(_workspace.ActiveSession);
        Assert.NotNull(_workspace.Explorer);
        Assert.Equal("Requête 1", Assert.Single(_workspace.Tabs).Title);
        Assert.Same(_workspace.Tabs[0], _workspace.ActiveTab);
    }

    [Theory]
    [InlineData(UiLanguage.French, "Requête 1")]
    [InlineData(UiLanguage.English, "Query 1")]
    public async Task Connect_FirstTabTitle_IsInTheChosenLanguage(UiLanguage language, string title)
    {
        using var _ = UiText.Override(language);
        await ConnectAsync();

        Assert.Equal(title, Assert.Single(_workspace.Tabs).Title);
    }

    [Fact]
    public async Task Connect_Fails_ShowsError()
    {
        _provider.Failure = new InvalidOperationException("nope");

        await ConnectAsync();

        Assert.Null(_workspace.ActiveSession);
        Assert.Equal("nope", Assert.Single(_dialogs.Errors).Message);
    }

    [Fact]
    public async Task NewTab_AddsUniqueTitles()
    {
        await ConnectAsync();

        _workspace.NewTabCommand.Execute(null);

        Assert.Equal(["Requête 1", "Requête 2"], _workspace.Tabs.Select(t => t.Title));
    }

    [Fact]
    public async Task CloseTab_Modified_AsksConfirmation()
    {
        await ConnectAsync();
        _editors[0].Text = "SELECT 1";
        _dialogs.ConfirmResult = false;

        await _workspace.CloseTabCommand.ExecuteAsync(_workspace.Tabs[0]);
        Assert.Single(_workspace.Tabs);

        _dialogs.ConfirmResult = true;
        await _workspace.CloseTabCommand.ExecuteAsync(_workspace.Tabs[0]);
        Assert.Empty(_workspace.Tabs);
        Assert.Null(_workspace.ActiveTab);
    }

    [Fact]
    public async Task CloseTab_Unmodified_ClosesWithoutAsking()
    {
        await ConnectAsync();
        _dialogs.ConfirmResult = false;

        await _workspace.CloseTabCommand.ExecuteAsync(_workspace.Tabs[0]);

        Assert.Empty(_workspace.Tabs);
    }

    [Fact]
    public async Task OpenTable_CreatesTabAndRunsSelectTop()
    {
        await ConnectAsync();

        await _workspace.OpenTableCommand.ExecuteAsync(Users);

        var expected = SqlSnippets.SelectTop(Users, DatabaseDialect.PostgreSql);
        Assert.Equal(expected, _editors[1].Text);
        Assert.Equal([expected], _provider.Executed);
        Assert.False(_workspace.ActiveTab!.IsModified);
    }

    [Fact]
    public async Task Disconnect_ClosesSessionAndItsTabs()
    {
        await ConnectAsync();
        _workspace.NewTabCommand.Execute(null);

        await _workspace.DisconnectCommand.ExecuteAsync(null);

        Assert.Empty(_workspace.Tabs);
        Assert.Empty(_workspace.Sessions);
        Assert.Null(_workspace.ActiveSession);
        Assert.All(_workspace.Explorer.Roots, r => Assert.False(r.IsConnected));
    }

    [Fact]
    public void AppCompositionCreate_TempDirectory_BuildsWorkspace()
    {
        var dir = Path.Combine(Path.GetTempPath(), "squill-" + Guid.NewGuid().ToString("N"));
        // Create applies the language of the OS, which must not leak into the other tests.
        var language = UiText.Language;
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var services = AppComposition.Create(new Platform(new ProviderRegistry(), _dialogs), new AppPaths(dir));

            Assert.NotNull(services.Workspace.ConnectionList);
            Assert.NotNull(services.History);
            Assert.NotNull(services.Snippets);
            Assert.NotNull(services.Settings);
        }
        finally
        {
            UiText.Initialize(language);
            CultureInfo.DefaultThreadCurrentCulture = null;
            CultureInfo.DefaultThreadCurrentUICulture = null;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
            try
            {
                Directory.Delete(dir, true);
            }
            catch (IOException)
            {
            }
        }
    }

    private sealed class Platform(ProviderRegistry providers, IDialogService dialogs) : IPlatformServices
    {
        public IUiDispatcher Dispatcher { get; } = new ImmediateDispatcher();
        public IDialogService Dialogs { get; } = dialogs;
        public Func<ISqlEditor> EditorFactory { get; } = () => new StubSqlEditor();
        public ProviderRegistry Providers { get; } = providers;
    }

    private sealed class FakeStore : IProfileStore
    {
        public ValueTask<IReadOnlyList<ConnectionProfile>> ListAsync(CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<ConnectionProfile>>([]);
        public ValueTask SaveAsync(ConnectionProfile profile, string? password, string? sshSecret, CancellationToken ct) => ValueTask.CompletedTask;
        public ValueTask DeleteAsync(Guid id, CancellationToken ct) => ValueTask.CompletedTask;
        public ValueTask<string?> GetPasswordAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>(null);
        public ValueTask<string?> GetSshSecretAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>(null);
    }

    private sealed class FakeTunnels : ISshTunnelFactory
    {
        public ValueTask<ISshTunnel> OpenAsync(SshTunnelSettings settings, string? secret, string remoteHost, int remotePort, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class FakeHistory : IQueryHistoryStore
    {
        public ValueTask AppendAsync(HistoryEntry e, CancellationToken ct) => ValueTask.CompletedTask;
        public ValueTask<IReadOnlyList<HistoryEntry>> ListAsync(int max, CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<HistoryEntry>>([]);
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
        public Exception? Failure { get; set; }
        public List<string> Executed { get; } = [];
        public bool ReturnGrid { get; set; }
        public string? FailOn { get; set; }

        public TaskCompletionSource? Gate { get; set; }

        public async ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile profile, string? password, CancellationToken cancellationToken)
        {
            if (Gate is { } gate)
                await gate.Task;
            return Failure is { } failure ? throw failure : new FakeConnection(this);
        }
    }

    private sealed class FakeConnection(FakeProvider owner) : IDatabaseConnection
    {
        public string ServerVersion => "1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
        {
            owner.Executed.Add(sql);
            if (owner.FailOn is { } f && sql.Contains(f, StringComparison.Ordinal))
                throw new InvalidOperationException("boom");
            return ValueTask.FromResult<IResultReader>(new FakeReader(owner.ReturnGrid));
        }

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(owner.ReturnGrid ? new SchemaSnapshot(null, [GridTable]) : SchemaSnapshot.Empty);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeReader(bool grid) : IResultReader
    {
        private bool _done;

        public IReadOnlyList<ColumnInfo> Columns { get; } = grid ? [new ColumnInfo("id", "int", typeof(int), false, 0)] : [];
        public long RecordsAffected => -1;

        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken)
        {
            var rows = grid && !_done ? new object?[][] { [1] } : null;
            _done = true;
            return ValueTask.FromResult<object?[][]?>(rows);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CloseTab_WithPendingGridChanges_AsksFirst(bool accept)
    {
        using var lang = UiText.Override(UiLanguage.English);
        _provider.ReturnGrid = true;
        await ConnectAsync();
        var tab = _workspace.Tabs[0];
        _editors[0].Text = "SELECT * FROM g";
        await tab.RunCommand.ExecuteAsync(null);
        tab.Results[0].Editor!.TrySetCell(0, 0, "5");
        _dialogs.ConfirmResult = accept;

        await _workspace.CloseTabCommand.ExecuteAsync(tab);

        Assert.Contains(("Close tab", "Discard pending grid changes?"), _dialogs.Confirms);
        Assert.Equal(accept, !_workspace.Tabs.Contains(tab));
    }

    [Theory]
    [InlineData(UiLanguage.French, "Connexion impossible")]
    [InlineData(UiLanguage.English, "Connection failed")]
    public async Task Connect_Fails_HeadingIsLocalizedAndDetailVerbatim(UiLanguage language, string heading)
    {
        using var _ = UiText.Override(language);
        _provider.Failure = new InvalidOperationException("driver says no");

        await ConnectAsync();

        Assert.Equal((heading, "driver says no"), Assert.Single(_dialogs.Errors));
    }

    [Theory]
    [InlineData(UiLanguage.French, "Fermer l'onglet", "Abandonner les modifications de la grille en attente ?")]
    [InlineData(UiLanguage.English, "Close tab", "Discard pending grid changes?")]
    public async Task CloseTab_PendingGridChanges_DialogIsLocalized(UiLanguage language, string title, string message)
    {
        using var _ = UiText.Override(language);
        _provider.ReturnGrid = true;
        await ConnectAsync();
        var tab = _workspace.Tabs[0];
        _editors[0].Text = "SELECT * FROM g";
        await tab.RunCommand.ExecuteAsync(null);
        tab.Results[0].Editor!.TrySetCell(0, 0, "5");
        _dialogs.ConfirmResult = false;

        await _workspace.CloseTabCommand.ExecuteAsync(tab);

        Assert.Equal((title, message), Assert.Single(_dialogs.Confirms));
    }

    [Theory]
    [InlineData(UiLanguage.French, "Terminal", "Cette version ne prend pas en charge le terminal.")]
    [InlineData(UiLanguage.English, "Terminal", "This build has no terminal support.")]
    public async Task NewTerminal_WithoutHost_ShowsLocalizedUnavailableError(UiLanguage language, string title, string message)
    {
        using var _ = UiText.Override(language);

        await _workspace.NewTerminalCommand.ExecuteAsync(null);

        Assert.Equal((title, message), Assert.Single(_dialogs.Errors));
    }
}
