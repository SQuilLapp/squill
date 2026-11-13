using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Platform.Storage;
using Squill.Presentation.Connections;
using Squill.Presentation.Localization;
using Squill.Presentation.Results;
using Squill.Presentation.Sessions;

namespace Squill.Presentation.Explorer;

/// <summary>A table or view picked in the explorer, with the session it belongs to.</summary>
/// <param name="Session">The session of the node's connection.</param>
/// <param name="Table">The table or view.</param>
public sealed record TableRequest(ConnectionSession Session, TableInfo Table);

/// <summary>A script the explorer asks to open in a new query tab, with the session it belongs to.</summary>
/// <param name="Session">The session of the node's connection.</param>
/// <param name="Text">The SQL text; it is only opened, never run.</param>
public sealed record ScriptRequest(ConnectionSession Session, string Text);

/// <summary>
/// The explorer as one filterable tree: saved connections (grouped by folder) at the top, and under each connected one
/// its database, schemas, tables, views and columns.
/// </summary>
public sealed partial class ExplorerTreeViewModel : ViewModelBase
{
    private readonly ConnectionListViewModel _list;
    private readonly ObservableCollection<ConnectionSession> _sessions;
    private readonly HashSet<Guid> _expanded = [];
    private readonly ITableFilterStore? _filters;
    private readonly Dictionary<Guid, Dictionary<string, HashSet<string>>> _hidden = [];

    /// <summary>Creates the tree and keeps it in step with the saved connections and the open sessions.</summary>
    /// <param name="list">The saved connections.</param>
    /// <param name="sessions">The open sessions.</param>
    /// <param name="filters">Persists the tables the user hides, per connection and database; <see langword="null"/> keeps them for the run only.</param>
    public ExplorerTreeViewModel(ConnectionListViewModel list, ObservableCollection<ConnectionSession> sessions, ITableFilterStore? filters = null)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(sessions);
        _list = list;
        _sessions = sessions;
        _filters = filters;
        list.Profiles.CollectionChanged += (_, _) => Rebuild();
        sessions.CollectionChanged += OnSessionsChanged;
        Rebuild();
    }

    /// <summary>Raised when the user opens a table or view.</summary>
    public event EventHandler<TableRequest>? OpenTableRequested;

    /// <summary>Raised when the user asks to design a table.</summary>
    public event EventHandler<TableRequest>? DesignTableRequested;

    /// <summary>Raised when the user asks to drop a table.</summary>
    public event EventHandler<TableRequest>? DropTableRequested;

    /// <summary>Raised when the user opens the definition of an object or scripts a table as <c>CREATE TABLE</c>.</summary>
    public event EventHandler<ScriptRequest>? ScriptRequested;

    /// <summary>Gets or sets the dialog that chooses the tables to show; set by the head, without it <see cref="ChooseTablesCommand"/> does nothing.</summary>
    public ITableVisibilityPicker? Picker { get; set; }

    /// <summary>Raised when the user activates a disconnected connection node.</summary>
    public event EventHandler<ConnectionProfile>? ConnectRequested;

    /// <summary>Gets the top-level nodes: connection folders and ungrouped connections.</summary>
    public ObservableCollection<ExplorerNode> Roots { get; } = [];

    /// <summary>Gets or sets the text that node titles must contain, ignoring case; empty shows everything.</summary>
    [ObservableProperty]
    public partial string FilterText { get; set; } = "";

    /// <summary>Gets or sets the selected node; selecting a node of a connection also selects that connection in the list.</summary>
    [ObservableProperty]
    public partial ExplorerNode? SelectedNode { get; set; }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    partial void OnSelectedNodeChanged(ExplorerNode? value)
    {
        if (value?.Profile is { } profile)
            _list.SelectedProfile = profile;
    }

    /// <summary>Marks a connection node as being opened or not, which shows or hides its progress indicator.</summary>
    /// <param name="profileId">The id of the profile being opened.</param>
    /// <param name="connecting">Whether the open is in progress.</param>
    public void SetConnecting(Guid profileId, bool connecting)
    {
        if (FindConnection(profileId) is { } node)
            node.IsConnecting = connecting;
    }

    /// <summary>Rebuilds the objects of one session from its current schema, for example after a DDL statement.</summary>
    /// <param name="session">The session whose schema changed.</param>
    public void Reload(ConnectionSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (FindConnection(session.Root.Profile.Id) is not { } node)
            return;
        if (node.Children.FirstOrDefault(c => c.Session == session) is { } database)
            FillDatabase(database, session);
        else if (session.Parent is null)
            Populate(node);
        ApplyFilter();
    }

    private void FillDatabase(ExplorerNode database, ConnectionSession session)
    {
        database.Children.Clear();
        var hidden = PopulateDatabase(database, session);
        database.HiddenCount = hidden;
        database.Detail = hidden > 0 ? UiText.FormatPlural("explorer.hiddenTables", hidden) : null;
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        ConnectionSession[] targets = SelectedNode?.Session is { } selected ? [selected] : [.. _sessions.SelectMany(s => s.OpenedDatabases.Prepend(s))];
        foreach (var session in targets)
        {
            await session.RefreshSchemaAsync(ct);
            Reload(session);
        }
    }

    [RelayCommand]
    private async Task ChooseTablesAsync(ExplorerNode? node, CancellationToken ct)
    {
        if (node?.Session is not { } session || Picker is not { } picker)
            return;
        var hidden = HiddenOf(session);
        var choices = session.Schema.Tables
            .OrderBy(t => t.Schema, StringComparer.OrdinalIgnoreCase).ThenBy(t => NameRank(t.Name)).ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Select(t => new TableChoice(KeyOf(t), t.Kind.IsViewLike(), hidden is null || !hidden.Contains(KeyOf(t))))
            .ToList();
        var shown = await picker.PickVisibleAsync(session.Profile.Database is { Length: > 0 } name ? name : session.Profile.Name, choices);
        if (shown is null)
            return;
        var visible = new HashSet<string>(shown, StringComparer.OrdinalIgnoreCase);
        SetHidden(session, choices.Select(c => c.Key).Where(k => !visible.Contains(k)), ct);
    }

    [RelayCommand]
    private void HideTable(ExplorerNode? node)
    {
        if (node is { Table: { } table, Session: { } session })
            SetHidden(session, (HiddenOf(session) ?? []).Append(KeyOf(table)), CancellationToken.None);
    }

    [RelayCommand]
    private void ShowAllTables(ExplorerNode? node)
    {
        if (node?.Session is { } session)
            SetHidden(session, [], CancellationToken.None);
    }

    [RelayCommand]
    private async Task RefreshDatabaseAsync(ExplorerNode? node, CancellationToken ct)
    {
        if (node is not { Kind: ExplorerNodeKind.Database, Session: { } session })
            return;
        await session.RefreshSchemaAsync(ct);
        Reload(session);
    }

    [RelayCommand]
    private void Activate(ExplorerNode? node)
    {
        if (node is { Kind: ExplorerNodeKind.Connection, Session: null, Profile: { } profile })
            ConnectRequested?.Invoke(this, profile);
        else if (node is { Kind: ExplorerNodeKind.Database, Session: null })
            node.IsExpanded = true; // Expanding opens the database.
        else if (node is { Kind: ExplorerNodeKind.Table or ExplorerNodeKind.View or ExplorerNodeKind.MaterializedView, Table: { } table, Session: { } session })
            OpenTableRequested?.Invoke(this, new TableRequest(session, table));
    }

    [RelayCommand]
    private void OpenDefinition(ExplorerNode? node)
    {
        if (node?.Session is not { } session)
            return;
        var dialect = session.Profile.Dialect;
        var text = node switch
        {
            { Kind: ExplorerNodeKind.View or ExplorerNodeKind.MaterializedView, Table: { } view } => DefinitionScript.View(dialect, view),
            { Kind: ExplorerNodeKind.Function or ExplorerNodeKind.Procedure, Routine: { } routine } => DefinitionScript.Routine(dialect, routine),
            { Kind: ExplorerNodeKind.Trigger, Trigger: { } trigger } => DefinitionScript.Trigger(dialect, trigger),
            _ => null,
        };
        if (text is not null)
            ScriptRequested?.Invoke(this, new ScriptRequest(session, text));
    }

    [RelayCommand]
    private void ScriptCreateTable(ExplorerNode? node)
    {
        if (node is { Kind: ExplorerNodeKind.Table, Table: { } table, Session: { } session })
            ScriptRequested?.Invoke(this, new ScriptRequest(session, DefinitionScript.CreateTable(session.Profile.Dialect, table)));
    }

    [RelayCommand]
    private void Design(ExplorerNode? node)
    {
        if (node is { Kind: ExplorerNodeKind.Table, Table: { } table, Session: { } session })
            DesignTableRequested?.Invoke(this, new TableRequest(session, table));
    }

    [RelayCommand]
    private void Drop(ExplorerNode? node)
    {
        if (node is { Kind: ExplorerNodeKind.Table, Table: { } table, Session: { } session })
            DropTableRequested?.Invoke(this, new TableRequest(session, table));
    }

    private void OnSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is not (NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Remove))
        {
            Rebuild();
            return;
        }
        foreach (var session in e.OldItems?.OfType<ConnectionSession>() ?? [])
        {
            if (FindConnection(session.Profile.Id) is not { } node)
                continue;
            node.Session = null;
            node.Children.Clear();
            node.IsExpanded = false;
            _expanded.Remove(session.Profile.Id);
        }
        foreach (var session in e.NewItems?.OfType<ConnectionSession>() ?? [])
        {
            if (FindConnection(session.Profile.Id) is not { } node)
                continue;
            node.Session = session;
            Populate(node);
            node.IsExpanded = true;
            _expanded.Add(session.Profile.Id);
        }
        ApplyFilter();
    }

    private void Rebuild()
    {
        foreach (var node in Connections())
        {
            if (node.IsExpanded)
                _expanded.Add(node.Profile!.Id);
            else
                _expanded.Remove(node.Profile!.Id);
        }
        Roots.Clear();
        foreach (var group in _list.Groups)
        {
            if (group.Folder is null)
            {
                foreach (var profile in group.Items)
                    Roots.Add(ConnectionNode(profile));
                continue;
            }
            var folder = new ExplorerNode(group.Folder, ExplorerNodeKind.ConnectionFolder, DisplayFormat.Count(group.Items.Count)) { IsExpanded = true };
            foreach (var profile in group.Items)
                folder.Children.Add(ConnectionNode(profile));
            Roots.Add(folder);
        }
        ApplyFilter();
    }

    private ExplorerNode ConnectionNode(ConnectionProfile profile)
    {
        var node = new ExplorerNode(profile.Name, ExplorerNodeKind.Connection) { Profile = profile };
        if (_sessions.FirstOrDefault(s => s.Profile.Id == profile.Id) is { } session)
        {
            node.Session = session;
            Populate(node);
            node.IsExpanded = _expanded.Contains(profile.Id);
        }
        return node;
    }

    private IEnumerable<ExplorerNode> Connections() =>
        Roots.SelectMany(r => r.Kind == ExplorerNodeKind.Connection ? [r] : r.Children);

    private ExplorerNode? FindConnection(Guid id) => Connections().FirstOrDefault(n => n.Profile!.Id == id);

    private void Populate(ExplorerNode connection)
    {
        connection.Children.Clear();
        if (connection.Session is not { } session)
            return;
        var profile = session.Profile;
        var database = new ExplorerNode(profile.Database is { Length: > 0 } name ? name : profile.Name, ExplorerNodeKind.Database)
        {
            Profile = profile,
            Session = session,
            IsExpanded = true,
        };
        FillDatabase(database, session);
        connection.Children.Add(database);
        foreach (var other in session.Databases.Where(d => !string.Equals(d, profile.Database, StringComparison.OrdinalIgnoreCase)))
        {
            var node = new ExplorerNode(other, ExplorerNodeKind.Database) { Profile = profile with { Database = other } };
            if (session.FindDatabase(other) is { } opened)
            {
                node.Session = opened;
                FillDatabase(node, opened);
                node.IsExpanded = true;
            }
            else
            {
                // A node without children shows no expander in either tree control, so the database could never be opened.
                node.Children.Add(new ExplorerNode("…", ExplorerNodeKind.Folder));
            }
            node.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ExplorerNode.IsExpanded) && node.IsExpanded && node.Session is null)
                    _ = OpenDatabaseAsync(node, session);
            };
            connection.Children.Add(node);
        }

        LoadFilter(profile.Id); // last: a filter that loads at once reloads the nodes added above
    }

    private async Task OpenDatabaseAsync(ExplorerNode node, ConnectionSession parent)
    {
        node.ErrorText = null;
        try
        {
            var opened = await parent.OpenDatabaseAsync(node.Title, CancellationToken.None);
            // Populate before publishing the session: a session set means the children are there.
            FillDatabase(node, opened);
            node.Session = opened;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            node.ErrorText = ex.Message;
            node.IsExpanded = false;
        }
    }

    // Fills the database node and returns how many tables and views the user hid.
    private int PopulateDatabase(ExplorerNode database, ConnectionSession session)
    {
        var snapshot = session.Schema;
        var hidden = HiddenOf(session);
        var allTables = snapshot.Tables;
        var shownTables = hidden is { Count: > 0 } ? allTables.Where(t => !hidden.Contains(KeyOf(t))).ToList() : allTables;
        var schemas = shownTables.Select(t => t.Schema ?? "").Concat(snapshot.Routines.Select(r => r.Schema)).Distinct(StringComparer.Ordinal).ToList();
        foreach (var key in schemas)
        {
            // Engines without schemas (SQLite) put their tables straight under the database.
            var parent = database;
            var tables = shownTables.Where(t => (t.Schema ?? "") == key).ToList();
            var routines = snapshot.Routines.Where(r => r.Schema == key).ToList();
            var triggers = snapshot.Triggers.Where(t => t.Schema == key).ToList();
            if (key.Length > 0)
            {
                parent = Node(key, ExplorerNodeKind.Schema, session, count: tables.Count + routines.Count + triggers.Count);
                parent.IsExpanded = schemas.Count == 1;
                database.Children.Add(parent);
            }
            AddGroup(parent, session, "explorer.tables", tables.Where(t => !t.Kind.IsViewLike()), schemas.Count == 1);
            AddGroup(parent, session, "explorer.views", tables.Where(t => t.Kind.IsViewLike()), false);
            AddFolder(parent, session, "explorer.routines", routines.Select(r => new ExplorerNode(r.Signature,
                r.Kind == RoutineKind.Procedure ? ExplorerNodeKind.Procedure : ExplorerNodeKind.Function, r.ReturnType)
                { Profile = session.Profile, Session = session, Routine = r }));
            AddFolder(parent, session, "explorer.triggers", triggers.Select(t => new ExplorerNode(t.Name, ExplorerNodeKind.Trigger, t.Table)
                { Profile = session.Profile, Session = session, Trigger = t }));
        }

        return allTables.Count - shownTables.Count;
    }

    private static string KeyOf(TableInfo table) => string.IsNullOrEmpty(table.Schema) ? table.Name : $"{table.Schema}.{table.Name}";

    private HashSet<string>? HiddenOf(ConnectionSession session) =>
        _hidden.TryGetValue(session.Profile.Id, out var databases) && databases.TryGetValue(session.Profile.Database ?? "", out var keys) ? keys : null;

    private void SetHidden(ConnectionSession session, IEnumerable<string> keys, CancellationToken ct)
    {
        var id = session.Profile.Id;
        if (!_hidden.TryGetValue(id, out var databases))
            _hidden[id] = databases = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        databases[session.Profile.Database ?? ""] = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
        Reload(session);
        if (_filters is not null)
        {
            var snapshot = new TableFilter(databases.Where(d => d.Value.Count > 0).ToDictionary(d => d.Key, d => (IReadOnlyList<string>)[.. d.Value.Order(StringComparer.OrdinalIgnoreCase)], StringComparer.OrdinalIgnoreCase));
            _ = SaveFilterAsync(id, snapshot, ct);
        }
    }

    private async Task SaveFilterAsync(Guid id, TableFilter filter, CancellationToken ct)
    {
        try
        {
            await _filters!.SaveAsync(id, filter, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A filter that cannot be saved still applies for this run.
        }
    }

    // The saved filter of a connection is read once, when its first session opens; the tree is rebuilt when it hides something.
    private void LoadFilter(Guid id)
    {
        if (_filters is null || _hidden.ContainsKey(id))
            return;
        _hidden[id] = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        _ = LoadFilterAsync(id);
    }

    private async Task LoadFilterAsync(Guid id)
    {
        var saved = await _filters!.LoadAsync(id, CancellationToken.None);
        if (saved is null || saved.Hidden.Count == 0)
            return;
        var databases = _hidden[id];
        foreach (var (database, keys) in saved.Hidden)
            databases.TryAdd(database, new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase));
        foreach (var session in _sessions.Where(s => s.Profile.Id == id).SelectMany(s => s.OpenedDatabases.Prepend(s)).ToList())
            Reload(session);
    }

    private static void AddGroup(ExplorerNode parent, ConnectionSession session, string titleKey, IEnumerable<TableInfo> tables, bool expanded)
    {
        var list = tables.OrderBy(t => NameRank(t.Name)).ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.Name, StringComparer.Ordinal).ToList();
        var folder = Node(UiText.Get(titleKey), ExplorerNodeKind.Folder, session, count: list.Count);
        foreach (var table in list)
            folder.Children.Add(TableNode(session, table));
        folder.IsExpanded = expanded;
        if (folder.Children.Count > 0)
            parent.Children.Add(folder);
    }

    // Names that start with an underscore come before the others, then A to Z.
    private static int NameRank(string name) => name.StartsWith('_') ? 0 : 1;

    private static void AddFolder(ExplorerNode parent, ConnectionSession session, string titleKey, IEnumerable<ExplorerNode> items)
    {
        var list = items.ToList();
        if (list.Count == 0)
            return;
        var folder = Node(UiText.Get(titleKey), ExplorerNodeKind.Folder, session, count: list.Count);
        foreach (var item in list)
            folder.Children.Add(item);
        parent.Children.Add(folder);
    }

    private static ExplorerNode Node(string title, ExplorerNodeKind kind, ConnectionSession session, string? detail = null, TableInfo? table = null, int? count = null) =>
        new(title, kind, detail, table, count) { Profile = session.Profile, Session = session };

    private static ExplorerNode TableNode(ConnectionSession session, TableInfo table)
    {
        var node = Node(table.Name, table.Kind switch { TableKind.View => ExplorerNodeKind.View, TableKind.MaterializedView => ExplorerNodeKind.MaterializedView, _ => ExplorerNodeKind.Table }, session, table: table);
        var columns = Node(UiText.Get("explorer.columns"), ExplorerNodeKind.Folder, session, count: table.Columns.Count);
        foreach (var c in table.Columns)
        {
            var detail = c.DataType + (c.IsPrimaryKey ? " PK" : "") + (c.IsNullable ? " NULL" : "");
            columns.Children.Add(Node(c.Name, ExplorerNodeKind.Column, session, detail));
        }
        var indexes = Node(UiText.Get("explorer.indexes"), ExplorerNodeKind.Folder, session, count: table.Indexes.Count);
        foreach (var i in table.Indexes)
            indexes.Children.Add(Node(i.Name, ExplorerNodeKind.Index, session, (i.IsUnique ? "UNIQUE " : "") + string.Join(", ", i.Columns)));
        var primary = table.Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name).ToList();
        var uniques = table.Indexes.Where(i => i.IsUnique).ToList();
        var uniqueKeys = Node(UiText.Get("explorer.keys"), ExplorerNodeKind.Folder, session, count: uniques.Count + (primary.Count > 0 ? 1 : 0));
        if (primary.Count > 0)
            uniqueKeys.Children.Add(Node(table.PrimaryKeyName ?? UiText.Get("explorer.primaryKey"), ExplorerNodeKind.Key, session, "PRIMARY " + string.Join(", ", primary)));
        foreach (var u in uniques)
            uniqueKeys.Children.Add(Node(u.Name, ExplorerNodeKind.Key, session, "UNIQUE " + string.Join(", ", u.Columns)));
        var keys = Node(UiText.Get("explorer.foreignKeys"), ExplorerNodeKind.Folder, session, count: table.ForeignKeys.Count);
        foreach (var k in table.ForeignKeys)
        {
            var target = k.ReferencedSchema is { Length: > 0 } s ? $"{s}.{k.ReferencedTable}" : k.ReferencedTable;
            keys.Children.Add(Node(k.Name, ExplorerNodeKind.ForeignKey, session, $"{string.Join(", ", k.Columns)} -> {target}"));
        }
        node.Children.Add(columns);
        node.Children.Add(indexes);
        node.Children.Add(uniqueKeys);
        node.Children.Add(keys);
        return node;
    }

    private void ApplyFilter()
    {
        var filter = FilterText.Trim();
        foreach (var root in Roots)
            Apply(root, filter);
    }

    private static bool Apply(ExplorerNode node, string filter)
    {
        var visible = filter.Length == 0 || node.Title.Contains(filter, StringComparison.OrdinalIgnoreCase);
        foreach (var child in node.Children)
            visible |= Apply(child, filter);
        node.IsVisible = visible;
        return visible;
    }
}
