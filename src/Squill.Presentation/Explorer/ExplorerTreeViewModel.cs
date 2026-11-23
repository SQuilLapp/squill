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

/// <summary>Several tables picked in the explorer, all of one session.</summary>
/// <param name="Session">The session of the tables' connection.</param>
/// <param name="Tables">The tables, in tree order.</param>
public sealed record TablesRequest(ConnectionSession Session, IReadOnlyList<TableInfo> Tables);

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
    private readonly Dictionary<Guid, ConnectionVisibility> _visibility = [];
    private readonly HashSet<Guid> _loaded = [];

    /// <summary>What the user chose to show for one connection: its databases, and per database its schemas and tables.</summary>
    private sealed class ConnectionVisibility
    {
        public NameVisibility Databases { get; set; } = new();

        public Dictionary<string, NameVisibility> Schemas { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, NameVisibility> Tables { get; } = new(StringComparer.OrdinalIgnoreCase);

        public static NameVisibility Of(Dictionary<string, NameVisibility> levels, string database)
        {
            if (!levels.TryGetValue(database, out var level))
                levels[database] = level = new NameVisibility();
            return level;
        }
    }

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

    /// <summary>Raised when the user asks to import a CSV file into a table.</summary>
    public event EventHandler<TableRequest>? ImportTableRequested;

    /// <summary>Raised when the user asks to drop a table.</summary>
    public event EventHandler<TableRequest>? DropTableRequested;

    /// <summary>Raised when the user asks to drop the selected tables.</summary>
    public event EventHandler<TablesRequest>? DropTablesRequested;

    /// <summary>Raised when the user opens the definition of an object or scripts a table as <c>CREATE TABLE</c>.</summary>
    public event EventHandler<ScriptRequest>? ScriptRequested;

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

    /// <summary>Gets the nodes the head reports as selected, without the ones no longer in the tree; <see cref="SelectedNode"/> is unaffected.</summary>
    public IReadOnlyList<ExplorerNode> SelectedNodes { get; private set; } = [];

    /// <summary>Gets the selected table nodes, in tree order.</summary>
    public IReadOnlyList<ExplorerNode> SelectedTables { get; private set; } = [];

    /// <summary>Gets how many tables are selected.</summary>
    public int SelectedTableCount => SelectedTables.Count;

    /// <summary>Replaces the selection; the heads call it on every selection change of their tree control.</summary>
    /// <param name="nodes">The selected nodes; the ones no longer in the tree are ignored.</param>
    public void SetSelectedNodes(IEnumerable<ExplorerNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var given = nodes.ToList();
        var picked = given.ToHashSet();
        var inTree = Walk(Roots).Where(n => n.IsVisible && picked.Contains(n)).ToList(); // a node hidden by the filter is no longer selected
        SelectedNodes = [.. given.Where(inTree.Contains)];
        SelectedTables = [.. inTree.Where(n => n is { Kind: ExplorerNodeKind.Table, Table: not null })];
        OnPropertyChanged(nameof(SelectedNodes));
        OnPropertyChanged(nameof(SelectedTables));
        OnPropertyChanged(nameof(SelectedTableCount));
        DropSelectedTablesCommand.NotifyCanExecuteChanged();
    }

    private static IEnumerable<ExplorerNode> Walk(IEnumerable<ExplorerNode> nodes) =>
        nodes.SelectMany(n => Walk(n.Children).Prepend(n));

    private bool CanDropSelectedTables() =>
        SelectedTables.Count >= 1 && SelectedTables.Count == SelectedNodes.Count
        && SelectedTables[0].Session is { } first && SelectedTables.All(n => n.Session == first);

    [RelayCommand(CanExecute = nameof(CanDropSelectedTables))]
    private void DropSelectedTables()
    {
        if (SelectedTables[0].Session is { } session)
            DropTablesRequested?.Invoke(this, new TablesRequest(session, [.. SelectedTables.Select(n => n.Table!)]));
    }

    /// <summary>Gets the text of the drop entry of the context menu of <paramref name="node"/>.</summary>
    /// <param name="node">The node the menu opened on.</param>
    /// <returns>The singular text, or the plural one with the count when the node is part of a selection of several tables.</returns>
    public string DropMenuText(ExplorerNode? node) =>
        InTableSelection(node) ? UiText.FormatPlural("explorer.dropTables", SelectedTables.Count) : UiText.Get("explorer.dropTable");

    /// <summary>Tells whether the drop entry of the context menu of <paramref name="node"/> can be used.</summary>
    /// <param name="node">The node the menu opened on.</param>
    /// <returns><see langword="false"/> when the selection that contains the node mixes kinds or sessions.</returns>
    public bool CanDropMenu(ExplorerNode? node) =>
        node is { Kind: ExplorerNodeKind.Table, Table: not null }
        && (!SelectedNodes.Contains(node) || SelectedNodes.Count == 1 || CanDropSelectedTables());

    private bool InTableSelection(ExplorerNode? node) => node is not null && SelectedTables.Count > 1 && SelectedTables.Contains(node);

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
        PopulateDatabase(database, session);
    }

    private static string Badge(int shown, int total) => UiText.Format("explorer.filterBadge", DisplayFormat.Count(shown), DisplayFormat.Count(total));

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
    private void HideTable(ExplorerNode? node)
    {
        if (node is { Table: { } table, Session: { } session })
        {
            TableLevel(session)?.Hide(KeyOf(table));
            Reload(session);
            SaveVisibility(session.Profile.Id);
        }
    }

    [RelayCommand]
    private void ShowAllTables(ExplorerNode? node)
    {
        if (node?.Session is { } session)
        {
            TableLevel(session)?.ShowAll(session.Schema.Tables.Select(KeyOf));
            Reload(session);
            SaveVisibility(session.Profile.Id);
        }
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
    private void Import(ExplorerNode? node)
    {
        if (node is { Kind: ExplorerNodeKind.Table, Table: { Kind: TableKind.Table } table, Session: { } session })
            ImportTableRequested?.Invoke(this, new TableRequest(session, table));
    }

    [RelayCommand]
    private void Drop(ExplorerNode? node)
    {
        if (InTableSelection(node))
        {
            if (CanDropSelectedTables())
                DropSelectedTables();
        }
        else if (node is { Kind: ExplorerNodeKind.Table, Table: { } table, Session: { } session })
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
        var databases = VisibilityOf(profile.Id).Databases;
        var total = 1;
        var shown = 0;
        // An engine without a database name (SQLite) always shows its one database.
        if (profile.Database is not { Length: > 0 } || databases.IsVisible(profile.Database))
        {
            var database = new ExplorerNode(profile.Database is { Length: > 0 } name ? name : profile.Name, ExplorerNodeKind.Database)
            {
                Profile = profile,
                Session = session,
                IsExpanded = true,
            };
            FillDatabase(database, session);
            connection.Children.Add(database);
            shown++;
        }
        foreach (var other in session.Databases.Where(d => !string.Equals(d, profile.Database, StringComparison.OrdinalIgnoreCase)))
        {
            total++;
            if (!databases.IsVisible(other))
                continue;
            shown++;
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

        connection.FilterBadge = total >= 2 ? Badge(shown, total) : null;
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

    // Fills the database node, sets its hidden-table count and its n of m badge.
    private void PopulateDatabase(ExplorerNode database, ConnectionSession session)
    {
        var snapshot = session.Schema;
        var visibility = VisibilityOf(session.Profile.Id);
        var dbName = session.Profile.Database ?? "";
        var tableLevel = ConnectionVisibility.Of(visibility.Tables, dbName);
        var schemaLevel = ConnectionVisibility.Of(visibility.Schemas, dbName);
        var allTables = snapshot.Tables;
        var shownTables = tableLevel.ShowsEverything ? allTables : allTables.Where(t => tableLevel.IsVisible(KeyOf(t))).ToList();
        var allSchemas = allTables.Select(t => t.Schema ?? "").Concat(snapshot.Routines.Select(r => r.Schema)).Distinct(StringComparer.Ordinal).ToList();
        var schemas = allSchemas.Where(k => k.Length == 0 || schemaLevel.IsVisible(k)).ToList();
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
                var inSchema = allTables.Count(t => t.Schema == key);
                parent.FilterBadge = inSchema > 0 ? Badge(tables.Count, inSchema) : null;
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

        database.HiddenCount = allTables.Count - shownTables.Count;
        var hasNamedSchemas = allSchemas.Exists(k => k.Length > 0);
        var (n, m) = hasNamedSchemas ? (schemas.Count, allSchemas.Count) : (shownTables.Count, allTables.Count);
        database.FilterBadge = m > 0 ? Badge(n, m) : null;
    }

    internal static string KeyOf(TableInfo table) => string.IsNullOrEmpty(table.Schema) ? table.Name : $"{table.Schema}.{table.Name}";

    /// <summary>Gets the names of the schemas of a snapshot, as the tree lists them (from its tables and routines); empty for an engine without schemas.</summary>
    internal static List<string> SchemaNames(SchemaSnapshot snapshot) =>
        [.. snapshot.Tables.Select(t => t.Schema ?? "").Concat(snapshot.Routines.Select(r => r.Schema)).Where(k => k.Length > 0).Distinct(StringComparer.Ordinal)];

    private FilterLevel? FilterOf(ExplorerNode? node)
    {
        if (node?.Session is not { } session)
            return null;
        switch (node.Kind)
        {
            case ExplorerNodeKind.Connection:
                return new(FilterMode.Databases, session, "");
            case ExplorerNodeKind.Database:
                return new(SchemaNames(session.Schema).Count > 0 ? FilterMode.Schemas : FilterMode.Tables, session, "");
            case ExplorerNodeKind.Schema:
                return new(FilterMode.Tables, session, node.Title);
            case ExplorerNodeKind.Folder when node.Title == UiText.Get("explorer.tables"):
                var parent = Walk(Roots).FirstOrDefault(n => n.Children.Contains(node));
                return parent?.Kind switch
                {
                    ExplorerNodeKind.Schema => new(FilterMode.Tables, session, parent.Title),
                    ExplorerNodeKind.Database => new(FilterMode.Tables, session, ""),
                    _ => null,
                };
            default:
                return null;
        }
    }

    /// <summary>Tells whether the filter popup can be opened from a node.</summary>
    /// <param name="node">A connection with a session, an opened database, a schema, or the Tables folder of one.</param>
    /// <returns><see langword="true"/> when <see cref="CreateFilter"/> would return a popup.</returns>
    public bool CanFilter(ExplorerNode? node) => FilterOf(node) is not null;

    /// <summary>Creates the popup that edits what the explorer shows under a node.</summary>
    /// <param name="node">A connection with a session (databases), an opened database (schemas, or tables without schemas), a schema or its Tables folder (tables).</param>
    /// <returns>The popup, or <see langword="null"/> for any other node or a disconnected connection.</returns>
    public ExplorerFilterViewModel? CreateFilter(ExplorerNode? node) => FilterOf(node) is { } level ? new ExplorerFilterViewModel(this, level) : null;

    private ConnectionVisibility VisibilityOf(Guid id)
    {
        if (!_visibility.TryGetValue(id, out var v))
            _visibility[id] = v = new ConnectionVisibility();
        return v;
    }

    /// <summary>Gets the live visibility of the databases of a connection, for the filter popup to edit.</summary>
    /// <param name="profileId">The id of the saved connection.</param>
    /// <returns>The level, created on demand; <see langword="null"/> for an unknown connection.</returns>
    public NameVisibility? DatabaseLevel(Guid profileId) => FindConnection(profileId) is null ? null : VisibilityOf(profileId).Databases;

    /// <summary>Gets the live visibility of the schemas of the database a session is on.</summary>
    /// <param name="session">The session of the database.</param>
    /// <returns>The level, created on demand; <see langword="null"/> for an unknown connection.</returns>
    public NameVisibility? SchemaLevel(ConnectionSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return FindConnection(session.Profile.Id) is null ? null : ConnectionVisibility.Of(VisibilityOf(session.Profile.Id).Schemas, session.Profile.Database ?? "");
    }

    /// <summary>Gets the live visibility of the tables (keys <c>schema.name</c>, or <c>name</c>) of the database a session is on.</summary>
    /// <param name="session">The session of the database.</param>
    /// <returns>The level, created on demand; <see langword="null"/> for an unknown connection.</returns>
    public NameVisibility? TableLevel(ConnectionSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return FindConnection(session.Profile.Id) is null ? null : ConnectionVisibility.Of(VisibilityOf(session.Profile.Id).Tables, session.Profile.Database ?? "");
    }

    /// <summary>Rebuilds the tree of a connection after its visibility levels changed, keeps the nodes the user opened, and saves the levels.</summary>
    /// <param name="profileId">The id of the saved connection.</param>
    public void ApplyVisibility(Guid profileId)
    {
        if (FindConnection(profileId) is not { } connection)
            return;
        var opened = new Dictionary<(string, string), bool>();
        foreach (var db in connection.Children)
        {
            opened[(db.Title, "")] = db.IsExpanded;
            foreach (var schema in db.Children.Where(c => c.Kind == ExplorerNodeKind.Schema))
                opened[(db.Title, schema.Title)] = schema.IsExpanded;
        }
        Populate(connection);
        foreach (var db in connection.Children)
        {
            if (db.Session is not null && opened.TryGetValue((db.Title, ""), out var dbOpen))
                db.IsExpanded = dbOpen;
            foreach (var schema in db.Children.Where(c => c.Kind == ExplorerNodeKind.Schema))
            {
                if (opened.TryGetValue((db.Title, schema.Title), out var open))
                    schema.IsExpanded = open;
            }
        }
        ApplyFilter();
        SaveVisibility(profileId);
    }

    // A level the user already changed while the file was loading wins over the saved one.
    private static void Merge(Dictionary<string, NameVisibility> levels, string database, NameVisibility saved)
    {
        if (!levels.TryGetValue(database, out var current) || current.ShowsEverything)
            levels[database] = saved;
    }

    private void ReloadConnection(Guid id)
    {
        foreach (var session in _sessions.Where(s => s.Profile.Id == id).SelectMany(s => s.OpenedDatabases.Prepend(s)).ToList())
            Reload(session);
    }

    private void SaveVisibility(Guid id)
    {
        if (_filters is null)
            return;
        var v = VisibilityOf(id);
        var hidden = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var patterns = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (db, level) in v.Tables)
        {
            if (level.ToSaved() is not { } saved)
                continue;
            if (saved.Hidden.Count > 0)
                hidden[db] = saved.Hidden;
            if (saved.Patterns.Count > 0)
                patterns[db] = saved.Patterns;
        }
        var schemas = new Dictionary<string, NameFilter>(StringComparer.OrdinalIgnoreCase);
        foreach (var (db, level) in v.Schemas)
        {
            if (level.ToSaved() is { } saved)
                schemas[db] = saved;
        }
        _ = SaveFilterAsync(id, new TableFilter(hidden) { Databases = v.Databases.ToSaved(), Schemas = schemas, TablePatterns = patterns }, CancellationToken.None);
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
        if (_filters is null || !_loaded.Add(id))
            return;
        _ = LoadFilterAsync(id);
    }

    private async Task LoadFilterAsync(Guid id)
    {
        var saved = await _filters!.LoadAsync(id, CancellationToken.None);
        if (saved is null)
            return;
        var v = VisibilityOf(id);
        if (saved.Databases is { } databases && v.Databases.ShowsEverything)
            v.Databases = new NameVisibility(databases.Hidden, databases.Patterns);
        foreach (var (db, filter) in saved.Schemas)
            Merge(v.Schemas, db, new NameVisibility(filter.Hidden, filter.Patterns));
        foreach (var db in saved.Hidden.Keys.Concat(saved.TablePatterns.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            saved.Hidden.TryGetValue(db, out var keys);
            saved.TablePatterns.TryGetValue(db, out var patterns);
            Merge(v.Tables, db, new NameVisibility(keys, patterns));
        }
        if (!v.Databases.ShowsEverything)
            ApplyVisibility(id);
        else if (v.Schemas.Values.Any(l => !l.ShowsEverything) || v.Tables.Values.Any(l => !l.ShowsEverything))
            ReloadConnection(id); // the nodes stay, only their children change
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
        SetSelectedNodes(SelectedNodes); // drop the selected nodes the filter just hid
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
