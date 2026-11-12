using Squill.Presentation.Localization;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Core.Transactions;
using Squill.Core.Design;
using Squill.Platform.Profiles;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation.Connections;
using Squill.Presentation.Designer;
using Squill.Presentation.Diagram;
using Squill.Presentation.Editor;
using Squill.Presentation.Explorer;
using Squill.Presentation.Plans;
using Squill.Presentation.Query;
using Squill.Presentation.Safety;
using Squill.Presentation.Sessions;
using Squill.Presentation.Terminal;
using Squill.Sql.Ddl;

namespace Squill.Presentation.Workspace;

/// <summary>The shell view-model tying the connection list, the open sessions, the object explorer and the query tabs together.</summary>
public sealed partial class WorkspaceViewModel : ViewModelBase, IDisposable
{
    private readonly SessionManager _manager;
    private readonly IQueryHistoryStore _history;
    private readonly ISettingsStore _settings;
    private readonly IUiDispatcher _dispatcher;
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
        TimeProvider? time = null)
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
        _elapsedTimer = (time ?? TimeProvider.System).CreateTimer(_ => _dispatcher.Post(RefreshElapsed), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        Explorer = new ExplorerTreeViewModel(connectionList, manager.Sessions);
        Explorer.OpenTableRequested += (_, r) => _ = OpenTableOnAsync(r.Session, r.Table);
        Explorer.DesignTableRequested += (_, r) => DesignTableOn(r.Session, r.Table);
        Explorer.DropTableRequested += (_, r) =>
        {
            ActiveSession = r.Session;
            _ = DropTableCommand.ExecuteAsync(r.Table);
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

    /// <summary>Gets whether any open session, or database opened from one, has a transaction that is open or failed.</summary>
    public bool HasOpenTransaction => AllSessions().Any(s => s.Transaction.State != TransactionState.None);

    /// <summary>Stops the timer that refreshes the duration of open transactions.</summary>
    public void Dispose() => _elapsedTimer.Dispose();

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

    /// <summary>Gets the environment of <see cref="ActiveSession"/>, or <see cref="ConnectionEnvironment.None"/> when not connected.</summary>
    public ConnectionEnvironment ActiveEnvironment => ActiveSession?.Profile.Environment ?? ConnectionEnvironment.None;

    partial void OnActiveSessionChanged(ConnectionSession? value)
    {
        OnPropertyChanged(nameof(ActiveEnvironment));
    }

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
            RemovePlansOf(tab);
        }
        foreach (var designer in Designers.Where(d => d.Session.Root == session).ToList())
            RemoveDesigner(designer);
        foreach (var diagram in Diagrams.Where(d => d.Session.Root == session).ToList())
            RemoveDiagram(diagram);
        foreach (var opened in session.OpenedDatabases.Prepend(session))
            _tabCounters.Remove(opened);
        await _manager.CloseAsync(session);
        ActiveSession = Sessions.LastOrDefault();
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
    private async Task DropTableAsync(TableInfo? table)
    {
        if (table is null || ActiveSession is not { } session)
            return;
        var name = table.Schema is { Length: > 0 } s ? $"{s}.{table.Name}" : table.Name;
        if (!await _dialogs.ConfirmAsync(UiText.Get("workspace.dropTable"), UiText.Format("workspace.dropTableConfirm", name)))
            return;
        try
        {
            foreach (var sql in DdlGenerators.For(session.Profile.Dialect).DropTable(table.Schema, table.Name))
            {
                var reader = await session.Connection.ExecuteAsync(sql, [], CancellationToken.None);
                try
                {
                    while (await reader.ReadBatchAsync(100, CancellationToken.None) is not null)
                    {
                    }
                }
                finally
                {
                    await reader.DisposeAsync();
                }
            }
            await session.RefreshSchemaAsync(CancellationToken.None);
            Explorer.Reload(session);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync(UiText.Get("workspace.dropFailed"), ex.Message);
            return;
        }
        foreach (var designer in Designers.Where(d => d.Session == session && d.Original is { } o && Matches(o, table)).ToList())
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

    private QueryTabViewModel AddTab(ConnectionSession session, string text)
    {
        var editor = _editorFactory();
        editor.Text = text;
        _tabCounters[session] = _tabCounters.GetValueOrDefault(session) + 1;
        var tab = new QueryTabViewModel(session, editor, _history, _settings, _dispatcher, _dialogs)
        {
            Title = session.Parent is null
                ? UiText.Format("tab.query", _tabCounters[session])
                : UiText.Format("tab.queryOn", _tabCounters[session], session.Profile.Name, session.Profile.Database),
        };
        tab.ScriptRequested += script => AddTab(session, script);
        tab.PlanReady += OpenPlan;
        Tabs.Add(tab);
        ActiveTab = tab;
        return tab;
    }
}
