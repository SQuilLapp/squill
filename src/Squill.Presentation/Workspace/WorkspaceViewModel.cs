using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation.Connections;
using Squill.Presentation.Editor;
using Squill.Presentation.Explorer;
using Squill.Presentation.Query;
using Squill.Presentation.Sessions;

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
    private readonly Dictionary<ConnectionSession, int> _tabCounters = [];

    /// <summary>Creates the workspace.</summary>
    /// <param name="manager">Opens and closes the sessions.</param>
    /// <param name="connectionList">The saved connections.</param>
    /// <param name="history">Handed to every query tab.</param>
    /// <param name="settings">Handed to every query tab.</param>
    /// <param name="dispatcher">Handed to every query tab.</param>
    /// <param name="dialogs">Used for confirmations and errors, and handed to every query tab.</param>
    /// <param name="editorFactory">Creates the head's editor for a new tab.</param>
    public WorkspaceViewModel(
        SessionManager manager,
        ConnectionListViewModel connectionList,
        IQueryHistoryStore history,
        ISettingsStore settings,
        IUiDispatcher dispatcher,
        IDialogService dialogs,
        Func<ISqlEditor> editorFactory)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(connectionList);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(editorFactory);
        _manager = manager;
        ConnectionList = connectionList;
        _history = history;
        _settings = settings;
        _dispatcher = dispatcher;
        _dialogs = dialogs;
        _editorFactory = editorFactory;
    }

    /// <summary>Gets the saved connections.</summary>
    public ConnectionListViewModel ConnectionList { get; }

    /// <summary>Gets the open sessions.</summary>
    public ObservableCollection<ConnectionSession> Sessions => _manager.Sessions;

    /// <summary>Gets the open query tabs of all sessions.</summary>
    public ObservableCollection<QueryTabViewModel> Tabs { get; } = [];

    /// <summary>Gets or sets the session shown in the explorer and used by new tabs.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(NewTabCommand))]
    public partial ConnectionSession? ActiveSession { get; set; }

    /// <summary>Gets the explorer of <see cref="ActiveSession"/>, or <see langword="null"/> when none is active.</summary>
    [ObservableProperty]
    public partial ObjectExplorerViewModel? Explorer { get; private set; }

    /// <summary>Gets or sets the tab shown.</summary>
    [ObservableProperty]
    public partial QueryTabViewModel? ActiveTab { get; set; }

    partial void OnActiveSessionChanged(ConnectionSession? value)
    {
        if (Explorer is { } old)
            old.OpenTableRequested -= OnOpenTableRequested;
        Explorer = value is null ? null : new ObjectExplorerViewModel(value);
        if (Explorer is { } created)
            created.OpenTableRequested += OnOpenTableRequested;
    }

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
        var index = Tabs.IndexOf(tab);
        Tabs.RemoveAt(index);
        if (ActiveTab == tab)
            ActiveTab = Tabs.Count == 0 ? null : Tabs[Math.Min(index, Tabs.Count - 1)];
    }

    [RelayCommand]
    private async Task OpenTableAsync(TableInfo? table)
    {
        if (table is null || ActiveSession is not { } session)
            return;
        var tab = AddTab(session, SqlSnippets.SelectTop(table, session.Profile.Dialect));
        await tab.RunCommand.ExecuteAsync(null);
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
        Tabs.Add(tab);
        ActiveTab = tab;
        return tab;
    }
}
