using Squill.Presentation.Localization;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Core.Transactions;
using Squill.Core.Design;
using Squill.Platform.Profiles;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation.Activity;
using Squill.Presentation.Compare;
using Squill.Presentation.Connections;
using Squill.Presentation.Designer;
using Squill.Presentation.Diagram;
using Squill.Presentation.Editor;
using Squill.Presentation.Explorer;
using Squill.Presentation.Home;
using Squill.Presentation.Import;
using Squill.Presentation.Plans;
using Squill.Presentation.Query;
using Squill.Presentation.Safety;
using Squill.Presentation.Security;
using Squill.Presentation.Palette;
using Squill.Presentation.Sessions;
using Squill.Presentation.Terminal;
using Squill.Sql.Ddl;

namespace Squill.Presentation.Workspace;

/// <summary>The shell view-model tying the connection list, the open sessions, the object explorer and the query tabs together.</summary>
public sealed partial class WorkspaceViewModel : ViewModelBase, IDisposable, IPaletteEditorHost
{
    private readonly SessionManager _manager;
    private readonly IQueryHistoryStore _history;
    private readonly ISettingsStore _settings;
    private readonly IUiDispatcher _dispatcher;
    private readonly TimeProvider _time;
    private readonly IDialogService _dialogs;
    private readonly Func<ISqlEditor> _editorFactory;
    private readonly IProfileStore _profiles;
    private readonly Func<ITerminalHost>? _terminalHostFactory;
    private readonly Func<string, string?> _findOnPath;
    private readonly IDiagramLayoutStore? _diagramStore;
    private readonly Func<string, double>? _measureText;
    private readonly Dictionary<ConnectionSession, int> _tabCounters = [];
    private readonly ITimer _elapsedTimer;

    /// <summary>Creates the workspace.</summary>
    /// <param name="manager">Opens and closes the sessions.</param>
    /// <param name="connectionList">The saved connections.</param>
    /// <param name="history">Handed to every query tab.</param>
    /// <param name="settings">Handed to every query tab.</param>
    /// <param name="dispatcher">Handed to every query tab.</param>
    /// <param name="dialogs">Used for confirmations and errors, and handed to every query tab.</param>
    /// <param name="editorFactory">Creates the head's editor for a new tab.</param>
    /// <param name="profiles">Source of the database password handed to CLI terminals.</param>
    /// <param name="terminalHostFactory">Creates the head's terminal host; <see langword="null"/> when the head has no terminal.</param>
    /// <param name="findOnPath">Resolves CLI executables; <see langword="null"/> searches the <c>PATH</c>.</param>
    /// <param name="diagramStore">Persists ER diagram layouts; <see langword="null"/> disables the diagram.</param>
    /// <param name="measureText">Text measurer of the head's diagram font; <see langword="null"/> disables the diagram.</param>
    /// <param name="time">Clock of the 30-second refresh of the transaction duration; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="tableFilters">Persists the tables the explorer hides; <see langword="null"/> keeps them for the run only.</param>
    /// <param name="recent">Remembers when each connection was opened for the home page; <see langword="null"/> keeps no history.</param>
    public WorkspaceViewModel(
        SessionManager manager,
        ConnectionListViewModel connectionList,
        IQueryHistoryStore history,
        ISettingsStore settings,
        IUiDispatcher dispatcher,
        IDialogService dialogs,
        Func<ISqlEditor> editorFactory,
        IProfileStore profiles,
        Func<ITerminalHost>? terminalHostFactory = null,
        Func<string, string?>? findOnPath = null,
        IDiagramLayoutStore? diagramStore = null,
        Func<string, double>? measureText = null,
        TimeProvider? time = null,
        ITableFilterStore? tableFilters = null,
        IRecentConnectionsStore? recent = null)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(connectionList);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(editorFactory);
        ArgumentNullException.ThrowIfNull(profiles);
        _manager = manager;
        ConnectionList = connectionList;
        _history = history;
        _settings = settings;
        _dispatcher = dispatcher;
        _dialogs = dialogs;
        _editorFactory = editorFactory;
        _profiles = profiles;
        _terminalHostFactory = terminalHostFactory;
        _findOnPath = findOnPath ?? TerminalLauncher.FindOnPath;
        _diagramStore = diagramStore;
        _measureText = measureText;
        // shortcut: the timer runs for the life of the workspace instead of starting when a transaction opens; the tick is one cheap pass.
        _time = time ?? TimeProvider.System;
        _elapsedTimer = _time.CreateTimer(_ => _dispatcher.Post(RefreshElapsed), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        var objects = new PaletteObjectProvider(this);
        var historySearch = new PaletteHistoryProvider(history, profiles, this, time);
        var line = new PaletteLineProvider(this);
        Palette = new PaletteViewModel(
            PaletteActionRegistry.ForWorkspace(this), objects.Search, historySearch.Search, historySearch.ReloadAsync, line.Search,
            () => AllSessions().Any(), dispatcher, dialogs);
        Explorer = new ExplorerTreeViewModel(connectionList, manager.Sessions, tableFilters);
        Home = new HomeViewModel(connectionList, recent ?? new NoRecentConnections(), settings, p => ConnectCommand.ExecuteAsync(p), Palette.Open, time ?? TimeProvider.System,
            id => (Tabs.Count(t => t.Session.Root.Profile.Id == id), AllSessions().FirstOrDefault(x => x.Profile.Id == id)?.Connection.ServerVersion));
        manager.Sessions.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is not null)
                foreach (var added in e.NewItems.OfType<ConnectionSession>().Where(x => x.Parent is null))
                    _ = Home.TouchAsync(added.Profile.Id, CancellationToken.None);
        };
        foreach (var collection in new INotifyCollectionChanged[] { manager.Sessions, Tabs, Terminals, Designers, Diagrams, Plans, Compares, Monitors, SecurityTabs })
            collection.CollectionChanged += (_, _) => UpdateHomeVisibility();
        foreach (var collection in new INotifyCollectionChanged[] { manager.Sessions, Tabs })
            collection.CollectionChanged += (_, _) => Home.RefreshLive();
        manager.Sessions.CollectionChanged += (_, _) => RaiseSchemaCompareAvailability();
        manager.Sessions.CollectionChanged += (_, _) => RaiseMonitorAvailability();
        manager.Sessions.CollectionChanged += (_, _) => RaiseSecurityAvailability();
        manager.Sessions.CollectionChanged += (_, _) => RaiseImportAvailability();
        Explorer.ImportTableRequested += (_, r) =>
        {
            ActiveSession = r.Session;
            _ = ImportOnAsync(r.Session, r.Table);
        };
        Explorer.OpenTableRequested += (_, r) => _ = OpenTableOnAsync(r.Session, r.Table);
        Explorer.DesignTableRequested += (_, r) => DesignTableOn(r.Session, r.Table);
        Explorer.DropTableRequested += (_, r) =>
        {
            ActiveSession = r.Session;
            _ = DropTableCommand.ExecuteAsync(r.Table);
        };
        Explorer.DropTablesRequested += (_, r) =>
        {
            ActiveSession = r.Session;
            _ = DropTablesCommand.ExecuteAsync(r.Tables);
        };
        Explorer.ScriptRequested += (_, r) => AddTab(r.Session, r.Text);
        Explorer.ConnectRequested += (_, profile) => _ = ConnectCommand.ExecuteAsync(profile);
        Explorer.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ExplorerTreeViewModel.SelectedNode) && Explorer.SelectedNode?.Session is { } session)
                ActiveSession = session;
        };
    }

    /// <summary>Gets the saved connections.</summary>
    public ConnectionListViewModel ConnectionList { get; }

    /// <summary>Gets the home page.</summary>
    public HomeViewModel Home { get; }

    /// <summary>Gets whether the home page is on screen: the startup setting at launch, then whenever no tab is open and no connection is active.</summary>
    [ObservableProperty]
    public partial bool IsHomeVisible { get; private set; }

    /// <summary>Loads the home page and decides whether it shows at launch; call once after the connection list is loaded.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the home page is ready.</returns>
    public async Task InitializeHomeAsync(CancellationToken ct)
    {
        await Home.InitializeAsync(ct);
        IsHomeVisible = Home.ShowAtStartup && HasNothingOpen();
    }

    private bool HasNothingOpen() =>
        Sessions.Count == 0 && Tabs.Count == 0 && Terminals.Count == 0 && Designers.Count == 0 && Diagrams.Count == 0 && Plans.Count == 0 && Compares.Count == 0 && Monitors.Count == 0 && SecurityTabs.Count == 0;

    private void UpdateHomeVisibility() => IsHomeVisible = HasNothingOpen();

    /// <summary>Gets the command palette; its <see cref="PaletteViewModel.Registry"/> takes the head-only actions.</summary>
    public PaletteViewModel Palette { get; }

    /// <summary>Opens the command palette; bound to <c>Ctrl+K</c> and to the title bar search box.</summary>
    [RelayCommand]
    private void OpenPalette() => Palette.Open();

    /// <summary>Gets whether any open session, or database opened from one, has a transaction that is open or failed.</summary>
    public bool HasOpenTransaction => AllSessions().Any(s => s.Transaction.State != TransactionState.None);

    /// <summary>Stops the timer that refreshes the duration of open transactions and the running palette search.</summary>
    public void Dispose()
    {
        _elapsedTimer.Dispose();
        foreach (var monitor in Monitors)
            monitor.Dispose();
        Palette.Dispose();
    }

    /// <summary>Asks what to do with every open transaction before the application exits; closes nothing itself.</summary>
    /// <param name="ct">Cancels a commit or rollback.</param>
    /// <returns><see langword="false"/> when the user went back or a commit or rollback failed, <see langword="true"/> when the application may exit.</returns>
    public async ValueTask<bool> PrepareExitAsync(CancellationToken ct)
    {
        foreach (var session in AllSessions().ToList())
        {
            if (!await TransactionPrompt.ResolveAsync(session, _dialogs, _history, "tx.ask.exit", ct))
                return false;
        }

        return true;
    }

    private IEnumerable<ConnectionSession> AllSessions() => Sessions.SelectMany(s => s.OpenedDatabases.Prepend(s)).Distinct();

    private void RefreshElapsed()
    {
        foreach (var session in AllSessions())
        {
            if (session.Transaction.State == TransactionState.Open)
                session.Transaction.RefreshElapsed();
        }

        foreach (var compare in Compares)
            compare.RefreshComparedText();
    }

    /// <summary>Gets the open sessions.</summary>
    public ObservableCollection<ConnectionSession> Sessions => _manager.Sessions;

    /// <summary>Gets the open query tabs of all sessions.</summary>
    public ObservableCollection<QueryTabViewModel> Tabs { get; } = [];

    /// <summary>Gets the open terminal tabs.</summary>
    public ObservableCollection<TerminalTabViewModel> Terminals { get; } = [];

    /// <summary>Gets the open table designer tabs.</summary>
    public ObservableCollection<TableDesignerViewModel> Designers { get; } = [];

    /// <summary>Gets the open ER diagram tabs, at most one per session.</summary>
    public ObservableCollection<DiagramTabViewModel> Diagrams { get; } = [];

    /// <summary>Gets the open plan tabs, at most one per query tab.</summary>
    public ObservableCollection<PlanTabViewModel> Plans { get; } = [];

    /// <summary>Gets the open schema compare tabs, at most one.</summary>
    public ObservableCollection<SchemaCompareViewModel> Compares { get; } = [];

    /// <summary>Gets or sets the session shown in the explorer and used by new tabs.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(NewTabCommand))]
    [NotifyCanExecuteChangedFor(nameof(NewTableCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenDiagramCommand))]
    public partial ConnectionSession? ActiveSession { get; set; }

    /// <summary>Gets the explorer tree of all connections; selecting one of its nodes makes that connection's session active.</summary>
    public ExplorerTreeViewModel Explorer { get; }

    /// <summary>Gets or sets the tab shown.</summary>
    [ObservableProperty]
    public partial QueryTabViewModel? ActiveTab { get; set; }

    /// <summary>Gets or sets the designer shown.</summary>
    [ObservableProperty]
    public partial TableDesignerViewModel? ActiveDesigner { get; set; }

    /// <summary>Gets or sets the diagram shown.</summary>
    [ObservableProperty]
    public partial DiagramTabViewModel? ActiveDiagram { get; set; }

    /// <summary>Gets or sets the plan tab shown.</summary>
    [ObservableProperty]
    public partial PlanTabViewModel? ActivePlan { get; set; }

    /// <summary>Gets the open sessions and locks monitors, at most one per connection session.</summary>
    public ObservableCollection<SessionsMonitorViewModel> Monitors { get; } = [];

    /// <summary>Gets or sets the sessions monitor tab shown.</summary>
    [ObservableProperty]
    public partial SessionsMonitorViewModel? ActiveMonitor { get; set; }

    private bool CanOpenSessionsMonitor() => SessionsMonitorViewModel.DisabledReason(ActiveSession).Length == 0;

    /// <summary>Gets why the sessions monitor cannot be opened (the tooltip of the disabled menu item and palette action), empty when it can.</summary>
    public string SessionsMonitorDisabledReason => SessionsMonitorViewModel.DisabledReason(ActiveSession);

    private void RaiseMonitorAvailability()
    {
        OpenSessionsMonitorCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SessionsMonitorDisabledReason));
    }

    /// <summary>Opens the sessions and locks monitor of the active session, or shows it when it is already open.</summary>
    [RelayCommand(CanExecute = nameof(CanOpenSessionsMonitor))]
    public void OpenSessionsMonitor()
    {
        if (ActiveSession is not { } session)
            return;
        if (Monitors.FirstOrDefault(m => m.Session == session) is { } open)
        {
            ActiveMonitor = open;
            return;
        }

        var monitor = new SessionsMonitorViewModel(session, _dialogs, _dispatcher);
        monitor.CloseRequested += RemoveMonitor;
        Monitors.Add(monitor);
        ActiveMonitor = monitor;
    }

    [RelayCommand]
    private void CloseMonitor(SessionsMonitorViewModel? monitor)
    {
        if (monitor is not null)
            RemoveMonitor(monitor);
    }

    private void RemoveMonitor(SessionsMonitorViewModel monitor)
    {
        var index = Monitors.IndexOf(monitor);
        if (index < 0)
            return;
        Monitors.RemoveAt(index);
        monitor.CloseRequested -= RemoveMonitor;
        monitor.Dispose();
        if (ActiveMonitor == monitor)
            ActiveMonitor = Monitors.Count == 0 ? null : Monitors[Math.Min(index, Monitors.Count - 1)];
    }

    /// <summary>Gets the open security tabs, at most one per connection session.</summary>
    public ObservableCollection<SecurityViewModel> SecurityTabs { get; } = [];

    /// <summary>Gets or sets the security tab shown.</summary>
    [ObservableProperty]
    public partial SecurityViewModel? ActiveSecurity { get; set; }

    private bool CanOpenSecurity() => SecurityViewModel.DisabledReason(ActiveSession).Length == 0;

    /// <summary>Gets why the security tab cannot be opened (the tooltip of the disabled menu item and palette action), empty when it can.</summary>
    public string SecurityDisabledReason => SecurityViewModel.DisabledReason(ActiveSession);

    private void RaiseSecurityAvailability()
    {
        OpenSecurityCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SecurityDisabledReason));
    }

    /// <summary>Opens the security tab of the active session, or shows it when it is already open.</summary>
    [RelayCommand(CanExecute = nameof(CanOpenSecurity))]
    public void OpenSecurity()
    {
        if (ActiveSession is not { } session)
            return;
        if (SecurityTabs.FirstOrDefault(s => s.Session == session) is { } open)
        {
            ActiveSecurity = open;
            return;
        }

        var security = new SecurityViewModel(session, _dialogs);
        security.CloseRequested += RemoveSecurity;
        SecurityTabs.Add(security);
        ActiveSecurity = security;
        _ = security.LoadAsync();
    }

    [RelayCommand]
    private void CloseSecurity(SecurityViewModel? security)
    {
        if (security is not null)
            RemoveSecurity(security);
    }

    private void RemoveSecurity(SecurityViewModel security)
    {
        var index = SecurityTabs.IndexOf(security);
        if (index < 0)
            return;
        SecurityTabs.RemoveAt(index);
        security.CloseRequested -= RemoveSecurity;
        security.Dispose();
        if (ActiveSecurity == security)
            ActiveSecurity = SecurityTabs.Count == 0 ? null : SecurityTabs[Math.Min(index, SecurityTabs.Count - 1)];
    }

    private bool CanImportData() => ImportWizardViewModel.DisabledReason(ActiveSession).Length == 0;

    /// <summary>Gets why the import wizard cannot be opened (the tooltip of the disabled menu item and palette action), empty when it can.</summary>
    public string ImportDisabledReason => ImportWizardViewModel.DisabledReason(ActiveSession);

    private void RaiseImportAvailability()
    {
        ImportDataCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ImportDisabledReason));
    }

    /// <summary>Opens the import wizard on the active session.</summary>
    /// <param name="table">The table to pre-select, or <see langword="null"/>.</param>
    /// <returns>A task that completes when the wizard window is closed.</returns>
    [RelayCommand(CanExecute = nameof(CanImportData))]
    public Task ImportDataAsync(TableInfo? table) => ActiveSession is { } session ? ImportOnAsync(session, table) : Task.CompletedTask;

    private async Task ImportOnAsync(ConnectionSession session, TableInfo? table)
    {
        if (table?.Kind is { } kind && kind != TableKind.Table)
            return;
        var wizard = new ImportWizardViewModel([.. AllSessions()], _dialogs, session, table, _dispatcher);
        wizard.ViewTableRequested += (s, t) => _ = OpenTableOnAsync(s, t);
        try
        {
            await _dialogs.ShowImportWizardAsync(wizard);
        }
        finally
        {
            wizard.Dispose();
        }
    }

    /// <summary>Gets or sets the schema compare tab shown.</summary>
    [ObservableProperty]
    public partial SchemaCompareViewModel? ActiveCompare { get; set; }

    private IReadOnlyList<CompareEndpoint> CompareEndpoints() => CompareEndpoint.List(AllSessions());

    private bool CanOpenSchemaCompare() => CompareEndpoint.CanCompare(CompareEndpoints());

    /// <summary>Gets why the schema comparison cannot be opened (the tooltip of the disabled menu item and palette action), empty when it can.</summary>
    public string SchemaCompareDisabledReason =>
        CanOpenSchemaCompare() ? ""
        : UiText.Get(!AllSessions().Any() ? "compare.disabled.sessions" : AllSessions().All(s => s.Profile.Dialect == DatabaseDialect.Oracle) ? "compare.disabled.oracle" : AllSessions().All(s => s.Profile.Dialect == DatabaseDialect.Informix) ? "compare.disabled.informix" : "compare.disabled.sessions");

    private void RaiseSchemaCompareAvailability()
    {
        OpenSchemaCompareCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SchemaCompareDisabledReason));
    }

    /// <summary>Opens the schema compare tab, or shows it when it is already open.</summary>
    /// <param name="sessionId">The profile id of the connection to compare from; <see langword="null"/> uses the active session.</param>
    [RelayCommand(CanExecute = nameof(CanOpenSchemaCompare))]
    public void OpenSchemaCompare(Guid? sessionId)
    {
        var from = sessionId is { } id ? AllSessions().FirstOrDefault(s => s.Profile.Id == id) : ActiveSession;
        if (Compares.FirstOrDefault() is { } open)
        {
            open.RefreshEndpoints();
            if (from is not null)
                open.SelectSource(from);
            ActiveCompare = open;
            return;
        }

        var compare = new SchemaCompareViewModel(() => AllSessions(), _dialogs);
        if (from is not null)
            compare.SelectSource(from);
        compare.CloseRequested += RemoveCompare;
        compare.ScriptRequested += (session, script) => AddTab(session, script);
        Compares.Add(compare);
        ActiveCompare = compare;
    }

    [RelayCommand]
    private void CloseCompare(SchemaCompareViewModel? compare)
    {
        if (compare is not null)
            RemoveCompare(compare);
    }

    private void RemoveCompare(SchemaCompareViewModel compare)
    {
        var index = Compares.IndexOf(compare);
        if (index < 0)
            return;
        Compares.RemoveAt(index);
        compare.CloseRequested -= RemoveCompare;
        compare.Dispose();
        if (ActiveCompare == compare)
            ActiveCompare = Compares.Count == 0 ? null : Compares[Math.Min(index, Compares.Count - 1)];
    }

    /// <summary>Shows a plan tab, adding it first when it is not open; a query tab keeps one plan tab whose content is replaced by each new plan.</summary>
    /// <param name="plan">The plan tab of a query tab.</param>
    public void OpenPlan(PlanTabViewModel plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!Plans.Contains(plan))
        {
            plan.CloseRequested += RemovePlan;
            Plans.Add(plan);
        }

        ActivePlan = plan;
    }

    [RelayCommand]
    private void ClosePlan(PlanTabViewModel? plan)
    {
        if (plan is not null)
            RemovePlan(plan);
    }

    private void RemovePlan(PlanTabViewModel plan)
    {
        var index = Plans.IndexOf(plan);
        if (index < 0)
            return;
        Plans.RemoveAt(index);
        plan.CloseRequested -= RemovePlan;
        if (ActivePlan == plan)
            ActivePlan = Plans.Count == 0 ? null : Plans[Math.Min(index, Plans.Count - 1)];
    }

    /// <summary>Gets the environment of the tab on screen, else of <see cref="ActiveSession"/>, or <see cref="ConnectionEnvironment.None"/> when not connected.</summary>
    public ConnectionEnvironment ActiveEnvironment => _shownCompare is { Environment: not ConnectionEnvironment.None } compare ? compare.Environment : (_shownSession ?? ActiveSession)?.Profile.Environment ?? ConnectionEnvironment.None;

    /// <summary>
    /// Records which tab is on screen, so the environment colour and the status bar follow the tab and not the explorer selection;
    /// the session of the tab also becomes <see cref="ActiveSession"/>, the one new tabs open on.
    /// </summary>
    /// <param name="tab">A query, designer, diagram or plan tab view-model; anything else (a terminal, <see langword="null"/>) has no session.</param>
    public void ShowTab(object? tab)
    {
        var session = tab switch
        {
            QueryTabViewModel q => q.Session,
            TableDesignerViewModel d => d.Session,
            DiagramTabViewModel g => g.Session,
            PlanTabViewModel p => p.Source.Session,
            SchemaCompareViewModel c => c.Session,
            SessionsMonitorViewModel m => m.Session,
            SecurityViewModel s => s.Session,
            _ => null,
        };
        foreach (var monitor in Monitors)
            monitor.SetActive(ReferenceEquals(monitor, tab)); // a monitor only polls while its tab is on screen
        foreach (var query in Tabs)
            query.SetActive(ReferenceEquals(query, tab)); // a query tab only auto-refreshes while shown
        _shownSession = session;
        _shownCompare = tab as SchemaCompareViewModel; // the target of a compare changes while its tab stays on screen
        if (session is not null && !ReferenceEquals(ActiveSession, session))
            ActiveSession = session; // raises ActiveEnvironment
        else
            OnPropertyChanged(nameof(ActiveEnvironment));
    }

    partial void OnActiveSessionChanged(ConnectionSession? value)
    {
        RaiseSchemaCompareAvailability();
        RaiseMonitorAvailability();
        RaiseSecurityAvailability();
        RaiseImportAvailability();
        if (value is null)
            _shownSession = null; // not connected: no tab can be on screen
        OnPropertyChanged(nameof(ActiveEnvironment));
    }

    partial void OnActiveTabChanged(QueryTabViewModel? value) => ShowTabIfAny(value);

    partial void OnActiveDesignerChanged(TableDesignerViewModel? value) => ShowTabIfAny(value);

    partial void OnActiveDiagramChanged(DiagramTabViewModel? value) => ShowTabIfAny(value);

    partial void OnActivePlanChanged(PlanTabViewModel? value) => ShowTabIfAny(value);

    partial void OnActiveCompareChanged(SchemaCompareViewModel? value) => ShowTabIfAny(value);

    partial void OnActiveMonitorChanged(SessionsMonitorViewModel? value) => ShowTabIfAny(value);

    partial void OnActiveSecurityChanged(SecurityViewModel? value) => ShowTabIfAny(value);

    private void ShowTabIfAny(object? tab)
    {
        if (tab is not null)
            ShowTab(tab);
    }

    private ConnectionSession? _shownSession;

    private SchemaCompareViewModel? _shownCompare;

    private bool HasSession() => ActiveSession is not null;

    [RelayCommand]
    private async Task ConnectAsync(ConnectionProfile? profile, CancellationToken ct)
    {
        if (profile is null)
            return;
        Explorer.SetConnecting(profile.Id, true);
        try
        {
            ActiveSession = await _manager.OpenAsync(profile, ct);
            AddTab(ActiveSession, "");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync(UiText.Get("workspace.connectionFailed"), ex.Message);
        }
        finally
        {
            Explorer.SetConnecting(profile.Id, false);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSession))]
    private async Task DisconnectAsync()
    {
        if (ActiveSession?.Root is not { } session)
            return;
        foreach (var opened in session.OpenedDatabases.Prepend(session).ToList())
        {
            if (!await TransactionPrompt.ResolveAsync(opened, _dialogs, _history, "tx.ask.disconnect", CancellationToken.None))
                return;
        }

        foreach (var tab in Tabs.Where(t => t.Session.Root == session).ToList())
        {
            Tabs.Remove(tab);
            tab.CancelPendingDiagnostics();
            tab.StopAutoRefresh();
            RemovePlansOf(tab);
        }
        foreach (var designer in Designers.Where(d => d.Session.Root == session).ToList())
            RemoveDesigner(designer);
        foreach (var diagram in Diagrams.Where(d => d.Session.Root == session).ToList())
            RemoveDiagram(diagram);
        foreach (var compare in Compares.Where(c => c.UsesServer(session)).ToList())
            RemoveCompare(compare);
        foreach (var monitor in Monitors.Where(m => m.Session?.Root == session).ToList())
            RemoveMonitor(monitor);
        foreach (var security in SecurityTabs.Where(s => s.Session?.Root == session).ToList())
            RemoveSecurity(security);
        foreach (var opened in session.OpenedDatabases.Prepend(session))
            _tabCounters.Remove(opened);
        await _manager.CloseAsync(session);
        if (_shownSession?.Root == session)
            _shownSession = null;
        ActiveSession = Sessions.LastOrDefault();
        OnPropertyChanged(nameof(ActiveEnvironment));
        ActiveTab = Tabs.LastOrDefault(t => t.Session == ActiveSession);
    }

    [RelayCommand(CanExecute = nameof(HasSession))]
    private void NewTab()
    {
        if (ActiveSession is { } session)
            AddTab(session, "");
    }

    [RelayCommand]
    private async Task CloseTabAsync(QueryTabViewModel? tab)
    {
        if (tab is null || !Tabs.Contains(tab))
            return;
        if (tab.IsModified && !await _dialogs.ConfirmAsync(UiText.Get("workspace.closeTab"), UiText.Format("workspace.closeTabConfirm", tab.Title)))
            return;
        if (tab.HasPendingChanges && !await _dialogs.ConfirmAsync(UiText.Get("workspace.closeTab"), UiText.Get("workspace.discardGridChanges")))
            return;
        if (!Tabs.Any(t => t != tab && t.Session == tab.Session)
            && !await TransactionPrompt.ResolveAsync(tab.Session, _dialogs, _history, "tx.ask.closeTab", CancellationToken.None))
            return;
        var index = Tabs.IndexOf(tab);
        Tabs.RemoveAt(index);
        tab.CancelPendingDiagnostics();
        tab.StopAutoRefresh();
        RemovePlansOf(tab);
        if (ActiveTab == tab)
            ActiveTab = Tabs.Count == 0 ? null : Tabs[Math.Min(index, Tabs.Count - 1)];
    }

    [RelayCommand]
    private async Task OpenTableAsync(TableInfo? table)
    {
        if (table is not null && ActiveSession is { } session)
            await OpenTableOnAsync(session, table);
    }

    private async Task OpenTableOnAsync(ConnectionSession session, TableInfo table)
    {
        var tab = AddTab(session, SqlSnippets.SelectTop(table, session.Profile.Dialect));
        await tab.RunCommand.ExecuteAsync(null);
    }

    [RelayCommand(CanExecute = nameof(HasSession))]
    private void NewTable()
    {
        if (ActiveSession is { } session)
            AddDesigner(session, null);
    }

    [RelayCommand]
    private void DesignTable(TableInfo? table)
    {
        if (table is not null && ActiveSession is { } session)
            DesignTableOn(session, table);
    }

    private void DesignTableOn(ConnectionSession session, TableInfo table)
    {
        if (table.Kind != TableKind.Table)
            return;
        var open = Designers.FirstOrDefault(d => d.Session == session && d.Original is { } o && Matches(o, table));
        if (open is null)
            AddDesigner(session, table);
        else
            ActiveDesigner = open;
    }

    private static bool Matches(TableDesign design, TableInfo table) =>
        string.Equals(design.Schema, table.Schema, StringComparison.OrdinalIgnoreCase)
        && string.Equals(design.Name, table.Name, StringComparison.OrdinalIgnoreCase);

    private void AddDesigner(ConnectionSession session, TableInfo? table)
    {
        if (!DdlGenerators.CanDesign(session.Profile.Dialect))
        {
            _ = _dialogs.ShowErrorAsync(UiText.Get("designer.title.new"), UiText.Get("designer.notAvailableInformix")).AsTask();
            return;
        }

        var designer = new TableDesignerViewModel(session, table, _dialogs, _dispatcher);
        designer.ScriptRequested += script => AddTab(session, script);
        Designers.Add(designer);
        ActiveDesigner = designer;
    }

    [RelayCommand]
    private async Task CloseDesignerAsync(TableDesignerViewModel? designer)
    {
        if (designer is null || !Designers.Contains(designer))
            return;
        if (designer.HasChanges && !await _dialogs.ConfirmAsync(UiText.Get("workspace.closeDesigner"), UiText.Get("workspace.discardUnsaved")))
            return;
        RemoveDesigner(designer);
    }

    /// <summary>Asks the user to confirm leaving when a table designer holds unsaved changes (before the app restarts to update).</summary>
    /// <returns><see langword="true"/> when nothing is unsaved or the user agreed to discard it.</returns>
    public async Task<bool> ConfirmCloseAsync() =>
        !Designers.Any(d => d.HasChanges)
        || await _dialogs.ConfirmAsync(UiText.Get("workspace.closeDesigner"), UiText.Get("workspace.discardUnsaved"));

    private void RemovePlansOf(QueryTabViewModel tab)
    {
        foreach (var plan in Plans.Where(p => p.Source == tab).ToList())
            RemovePlan(plan);
    }

    private void RemoveDesigner(TableDesignerViewModel designer)
    {
        var index = Designers.IndexOf(designer);
        if (index < 0)
            return;
        Designers.RemoveAt(index);
        if (ActiveDesigner == designer)
            ActiveDesigner = Designers.Count == 0 ? null : Designers[Math.Min(index, Designers.Count - 1)];
    }

    private bool CanOpenDiagram() => HasSession() && _diagramStore is not null && _measureText is not null;

    [RelayCommand(CanExecute = nameof(CanOpenDiagram))]
    private async Task OpenDiagramAsync()
    {
        if (ActiveSession is not { } session || _diagramStore is null || _measureText is null)
            return;
        if (Diagrams.FirstOrDefault(d => d.Session == session) is { } open)
        {
            ActiveDiagram = open;
            return;
        }
        var diagram = new DiagramTabViewModel(session, _diagramStore, _dialogs, _measureText, _dispatcher);
        diagram.OpenTableRequested += (_, table) => _ = OpenTableOnAsync(session, table);
        diagram.DesignTableRequested += (_, table) => DesignTableOn(session, table);
        Diagrams.Add(diagram);
        ActiveDiagram = diagram;
        try
        {
            await diagram.InitializeAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _dialogs.ShowErrorAsync(UiText.Get("workspace.layoutLoadFailed"), ex.Message);
        }
    }

    [RelayCommand]
    private void CloseDiagram(DiagramTabViewModel? diagram)
    {
        if (diagram is not null)
            RemoveDiagram(diagram);
    }

    private void RemoveDiagram(DiagramTabViewModel diagram)
    {
        var index = Diagrams.IndexOf(diagram);
        if (index < 0)
            return;
        Diagrams.RemoveAt(index);
        diagram.Dispose();
        if (ActiveDiagram == diagram)
            ActiveDiagram = Diagrams.Count == 0 ? null : Diagrams[Math.Min(index, Diagrams.Count - 1)];
    }

    [RelayCommand]
    private Task DropTableAsync(TableInfo? table) => table is null ? Task.CompletedTask : DropTablesCoreAsync([table]);

    [RelayCommand]
    private Task DropTablesAsync(IReadOnlyList<TableInfo>? tables) => tables is null ? Task.CompletedTask : DropTablesCoreAsync(tables);

    private static string QualifiedName(TableInfo t) => t.Schema is { Length: > 0 } s ? $"{s}.{t.Name}" : t.Name;

    private static string Listed(List<string> lines, int max) =>
        string.Join("\n", lines.Count <= max ? lines : lines.Take(max).Append(UiText.Format("prod.more", lines.Count - max)));

    private async Task DropTablesCoreAsync(IReadOnlyList<TableInfo> selected)
    {
        if (ActiveSession is not { } session)
            return;
        var tables = selected.Where(t => t.Kind == TableKind.Table).ToList();
        if (tables.Count == 0)
            return;
        var plan = TableDropPlan.Build(tables, session.Schema);
        if (plan.Blockers.Count > 0)
        {
            var lines = plan.Blockers.Select(b => $"{QualifiedName(b.Referencing)} -> {QualifiedName(b.Referenced)}").ToList();
            await _dialogs.ShowErrorAsync(UiText.Get("workspace.dropBlocked"), UiText.Format("workspace.dropBlockedMessage", Listed(lines, 10)));
            return;
        }

        var dialect = session.Profile.Dialect;
        var generator = DdlGenerators.For(dialect);
        var drops = plan.Order.Select(t => (Table: t, Statements: generator.DropTable(t.Schema, t.Name).ToList())).ToList();
        var statements = drops.SelectMany(d => d.Statements).ToList();
        var production = ProductionGuard.Evaluate(session.Profile, statements).Required;
        if (!production)
        {
            var confirmed = tables.Count == 1
                ? await _dialogs.ConfirmAsync(UiText.Get("workspace.dropTable"), UiText.Format("workspace.dropTableConfirm", QualifiedName(tables[0])))
                : await _dialogs.ConfirmAsync(UiText.Get("workspace.dropTables"),
                    UiText.Format("workspace.dropTablesConfirm", plan.Order.Count, Listed([.. plan.Order.Select(QualifiedName)], 15))
                    + "\n" + UiText.Get("workspace.dropViewsNote")
                    + (DdlGenerators.UsesTransactionalDdl(dialect) ? "" : "\n" + UiText.Get("workspace.dropNotAtomicNote")));
            if (!confirmed)
                return;
        }

        var tx = session.Transaction;
        var transactional = DdlGenerators.UsesTransactionalDdl(dialect);
        if (!transactional && tx.State == TransactionState.Open
            && !await _dialogs.ConfirmAsync(UiText.Get("tx.implicitCommit.title"), UiText.Get("tx.implicitCommit.message")))
            return;

        var label = tables.Count == 1 ? tables[0].Name : UiText.FormatPlural("workspace.dropTablesLabel", plan.Order.Count);
        if (!await ProductionRequestBuilder.ConfirmWriteAsync(_dialogs, session.Profile, ConfirmationKind.SchemaChange, statements, plan.Order.Count, label, tx.Mode))
            return;

        var dropped = new List<TableInfo>();
        var ran = false;
        string? failure = null;
        using (tx.BeginOperation())
        {
            StatementScope? scope = null;
            var failing = plan.Order[0];
            try
            {
                if (transactional)
                    scope = await SqlRunner.BeginScopeAsync(session.Connection, dialect, tx, UiText.Get("workspace.dropTables"), CancellationToken.None);
                foreach (var (table, sqls) in drops)
                {
                    failing = table;
                    foreach (var sql in sqls)
                    {
                        ran = true;
                        var rows = await SqlRunner.ExecuteAsync(session.Connection, sql, [], CancellationToken.None);
                        scope?.Observe(sql, rows);
                    }
                    dropped.Add(table);
                }
                if (scope is not null)
                    await scope.CompleteAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                if (scope is not null)
                {
                    await scope.FailAsync();
                    dropped.Clear(); // rolled back: nothing is gone
                }
                failure = !transactional && dropped.Count > 0
                    ? UiText.Format("workspace.dropPartial", dropped.Count, plan.Order.Count, QualifiedName(failing), ex.Message)
                    : ex.Message;
            }
        }

        if (ran)
        {
            try
            {
                await session.RefreshSchemaAsync(CancellationToken.None);
                Explorer.Reload(session);
            }
            catch (Exception ex)
            {
                failure ??= ex.Message;
            }
        }

        if (failure is not null)
            await _dialogs.ShowErrorAsync(UiText.Get("workspace.dropFailed"), failure);
        foreach (var designer in Designers.Where(d => d.Session == session && d.Original is { } o && dropped.Any(t => Matches(o, t))).ToList())
            RemoveDesigner(designer);
    }

    [RelayCommand]
    private async Task NewTerminalAsync(ConnectionProfile? profile, CancellationToken ct)
    {
        if (_terminalHostFactory is null)
        {
            await _dialogs.ShowErrorAsync(UiText.Get("workspace.terminal"), UiText.Get("workspace.terminalUnsupported"));
            return;
        }
        var launch = TerminalLauncher.LocalShell();
        string? reason = null;
        if (profile is not null)
        {
            var password = await _profiles.GetPasswordAsync(profile.Id, ct);
            int? tunnelPort = null;
            if (profile.Ssh is not null)
            {
                // The tunnel belongs to the open session and lives as long as it does.
                tunnelPort = _manager.Sessions.FirstOrDefault(s => s.Profile.Id == profile.Id)?.TunnelPort;
                if (tunnelPort is null)
                {
                    await _dialogs.ShowErrorAsync(UiText.Get("workspace.terminal"), UiText.Format("terminal.tunnelNotOpen", profile.Name));
                    return;
                }
            }
            var cli = TerminalLauncher.ForProfile(profile, password, _findOnPath, out reason, tunnelPort);
            if (cli is null)
            {
                await _dialogs.ShowErrorAsync(UiText.Get("workspace.terminal"), reason ?? UiText.Get("workspace.terminalNotStarted"));
                return;
            }
            launch = cli;
        }
        var tab = new TerminalTabViewModel(_terminalHostFactory(), launch);
        if (profile is not null)
            tab.Title = $"{profile.Name} ({tab.Title})";
        Terminals.Add(tab);
        tab.StartCommand.Execute(null);
    }

    [RelayCommand]
    private void CloseTerminal(TerminalTabViewModel? terminal)
    {
        if (terminal is null || !Terminals.Remove(terminal))
            return;
        terminal.Dispose();
    }

    /// <summary>Opens a new tab on the active session holding the given text, without running it.</summary>
    /// <param name="sql">Initial script text.</param>
    /// <returns>The new tab, or <see langword="null"/> when no session is active.</returns>
    public QueryTabViewModel? OpenInNewTab(string sql)
    {
        ArgumentNullException.ThrowIfNull(sql);
        return ActiveSession is { } session ? AddTab(session, sql) : null;
    }

    ISqlEditor? IPaletteEditorHost.ActiveEditor => ActiveTab?.Editor;

    IReadOnlyList<ConnectionSession> IPaletteObjectHost.Sessions => [.. AllSessions()];

    async ValueTask IPaletteObjectHost.OpenTableAsync(ConnectionSession session, TableInfo table)
    {
        ActiveSession = session;
        await OpenTableOnAsync(session, table);
    }

    void IPaletteObjectHost.OpenScript(ConnectionSession session, string text)
    {
        ActiveSession = session;
        AddTab(session, text, caretAtEnd: true);
    }

    private QueryTabViewModel AddTab(ConnectionSession session, string text, bool caretAtEnd = false)
    {
        var editor = _editorFactory();
        editor.Text = text;
        if (caretAtEnd)
            editor.CaretOffset = text.Length;
        _tabCounters[session] = _tabCounters.GetValueOrDefault(session) + 1;
        var tab = new QueryTabViewModel(session, editor, _history, _settings, _dispatcher, _dialogs, _time)
        {
            Title = session.Parent is null
                ? UiText.Format("tab.query", _tabCounters[session])
                : UiText.Format("tab.queryOn", _tabCounters[session], session.Profile.Name, session.Profile.Database),
        };
        tab.ScriptRequested += script => AddTab(session, script);
        tab.PlanReady += OpenPlan;
        tab.TableDefinitionRequested += (table, inDesigner) =>
        {
            if (inDesigner)
                DesignTableOn(session, table);
            else
                Explorer.RevealTable(session, table);
        };
        Tabs.Add(tab);
        ActiveTab = tab;
        return tab;
    }
}

/// <summary>The recent-connections store of a workspace built without one: remembers nothing.</summary>
internal sealed class NoRecentConnections : IRecentConnectionsStore
{
    public Task<IReadOnlyList<RecentConnection>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<RecentConnection>>([]);

    public Task TouchAsync(Guid profileId, DateTimeOffset openedAt, CancellationToken ct) => Task.CompletedTask;

    public Task ForgetAsync(Guid profileId, CancellationToken ct) => Task.CompletedTask;
}
