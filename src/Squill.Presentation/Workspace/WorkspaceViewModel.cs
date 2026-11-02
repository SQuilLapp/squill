using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Core.Design;
using Squill.Platform.Profiles;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation.Connections;
using Squill.Presentation.Designer;
using Squill.Presentation.Diagram;
using Squill.Presentation.Editor;
using Squill.Presentation.Explorer;
using Squill.Presentation.Query;
using Squill.Presentation.Sessions;
using Squill.Presentation.Terminal;
using Squill.Sql.Ddl;

namespace Squill.Presentation.Workspace;

/// <summary>The shell view-model tying the connection list, the open sessions, the object explorer and the query tabs together.</summary>
public sealed partial class WorkspaceViewModel : ViewModelBase
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
        Func<string, double>? measureText = null)
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
    }

    /// <summary>Gets the saved connections.</summary>
    public ConnectionListViewModel ConnectionList { get; }

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

    /// <summary>Gets or sets the session shown in the explorer and used by new tabs.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(NewTabCommand))]
    [NotifyCanExecuteChangedFor(nameof(NewTableCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenDiagramCommand))]
    public partial ConnectionSession? ActiveSession { get; set; }

    /// <summary>Gets the explorer of <see cref="ActiveSession"/>, or <see langword="null"/> when none is active.</summary>
    [ObservableProperty]
    public partial ObjectExplorerViewModel? Explorer { get; private set; }

    /// <summary>Gets or sets the tab shown.</summary>
    [ObservableProperty]
    public partial QueryTabViewModel? ActiveTab { get; set; }

    /// <summary>Gets or sets the designer shown.</summary>
    [ObservableProperty]
    public partial TableDesignerViewModel? ActiveDesigner { get; set; }

    /// <summary>Gets or sets the diagram shown.</summary>
    [ObservableProperty]
    public partial DiagramTabViewModel? ActiveDiagram { get; set; }

    partial void OnActiveSessionChanged(ConnectionSession? value)
    {
        if (Explorer is { } old)
        {
            old.OpenTableRequested -= OnOpenTableRequested;
            old.DesignTableRequested -= OnDesignTableRequested;
            old.DropTableRequested -= OnDropTableRequested;
        }
        Explorer = value is null ? null : new ObjectExplorerViewModel(value);
        if (Explorer is { } created)
        {
            created.OpenTableRequested += OnOpenTableRequested;
            created.DesignTableRequested += OnDesignTableRequested;
            created.DropTableRequested += OnDropTableRequested;
        }
    }

    private void OnDesignTableRequested(object? sender, TableInfo table) => DesignTableCommand.Execute(table);

    private void OnDropTableRequested(object? sender, TableInfo table) => _ = DropTableCommand.ExecuteAsync(table);

    private void OnOpenTableRequested(object? sender, TableInfo table) => _ = OpenTableCommand.ExecuteAsync(table);

    private bool HasSession() => ActiveSession is not null;

    [RelayCommand]
    private async Task ConnectAsync(ConnectionProfile? profile, CancellationToken ct)
    {
        if (profile is null)
            return;
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
            await _dialogs.ShowErrorAsync("Connection failed", ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSession))]
    private async Task DisconnectAsync()
    {
        if (ActiveSession is not { } session)
            return;
        foreach (var tab in Tabs.Where(t => t.Session == session).ToList())
            Tabs.Remove(tab);
        foreach (var designer in Designers.Where(d => d.Session == session).ToList())
            RemoveDesigner(designer);
        foreach (var diagram in Diagrams.Where(d => d.Session == session).ToList())
            RemoveDiagram(diagram);
        _tabCounters.Remove(session);
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
        if (tab.IsModified && !await _dialogs.ConfirmAsync("Close tab", $"Close '{tab.Title}' and discard its changes?"))
            return;
        if (tab.HasPendingChanges && !await _dialogs.ConfirmAsync("Close tab", "Discard pending grid changes?"))
            return;
        var index = Tabs.IndexOf(tab);
        Tabs.RemoveAt(index);
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
        if (designer.HasChanges && !await _dialogs.ConfirmAsync("Close designer", "Discard unsaved changes?"))
            return;
        RemoveDesigner(designer);
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
            await _dialogs.ShowErrorAsync("Loading the diagram layout failed", ex.Message);
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
        if (!await _dialogs.ConfirmAsync("Drop table", $"Drop {name}? This cannot be undone."))
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
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("Drop failed", ex.Message);
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
            await _dialogs.ShowErrorAsync("Terminal", "This build has no terminal support.");
            return;
        }
        var launch = TerminalLauncher.LocalShell();
        string? reason = null;
        if (profile is not null)
        {
            var password = await _profiles.GetPasswordAsync(profile.Id, ct);
            var cli = TerminalLauncher.ForProfile(profile, password, _findOnPath, out reason);
            if (cli is null)
            {
                await _dialogs.ShowErrorAsync("Terminal", reason ?? "The terminal could not be started.");
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
            Title = $"Query {_tabCounters[session]}",
        };
        tab.ScriptRequested += script => AddTab(session, script);
        Tabs.Add(tab);
        ActiveTab = tab;
        return tab;
    }
}
