using System.Collections.ObjectModel;
using System.Globalization;
using Squill.Core;
using Squill.Data;
using Squill.Platform.Profiles;
using Squill.Platform.Storage;
using Squill.Presentation.Connections;
using Squill.Presentation.Explorer;
using Squill.Presentation.Sessions;
using Squill.Presentation.Localization;
using Xunit;

namespace Squill.Presentation.Tests.Explorer;

public sealed class ExplorerTreeViewModelTests
{
    private static readonly ColumnDefinition Id = new("id", "int", false, true, null);
    private static readonly ColumnDefinition Email = new("email", "text", true, false, null);

    private readonly ConnectionListViewModel _list = new(new FakeStore(), new ProviderRegistry(), new FakeDialogService());
    private readonly ObservableCollection<ConnectionSession> _sessions = [];
    private readonly ExplorerTreeViewModel _tree;

    public ExplorerTreeViewModelTests() => _tree = new ExplorerTreeViewModel(_list, _sessions);

    private static TableInfo Table(string? schema, string name, TableKind kind = TableKind.Table) =>
        new(schema, name, kind, [Id, Email], [new IndexInfo("ix_email", true, ["email"])],
            [new ForeignKeyInfo("fk_org", ["id"], "public", "orgs", ["id"])]);

    private static ConnectionProfile Profile(string name, string? folder = null, string? database = "d") =>
        new(Guid.NewGuid(), name, DatabaseDialect.PostgreSql, "h", 1, database, "u", null, null, ConnectionEnvironment.None, folder);

    private static ConnectionSession Session(ConnectionProfile profile, SchemaSnapshot schema, SchemaSnapshot? next = null) =>
        new(profile, new FakeConnection(next ?? schema), schema, null);

    private ExplorerNode Connect(ConnectionProfile profile, params TableInfo[] tables)
    {
        _sessions.Add(Session(profile, new SchemaSnapshot(null, tables)));
        return _tree.Roots.Concat(_tree.Roots.SelectMany(r => r.Children)).Single(n => n.Profile == profile && n.Kind == ExplorerNodeKind.Connection);
    }

    private static IEnumerable<ExplorerNode> Descendants(ExplorerNode node) => node.Children.SelectMany(c => Descendants(c).Prepend(c));

    [Fact]
    public void Roots_GroupByFolder_UngroupedLast_ItemsByName()
    {
        _list.Profiles.Add(Profile("z"));
        _list.Profiles.Add(Profile("b", "Clients"));
        _list.Profiles.Add(Profile("a", "Clients"));

        Assert.Equal(["Clients", "z"], _tree.Roots.Select(r => r.Title));
        Assert.Equal([ExplorerNodeKind.ConnectionFolder, ExplorerNodeKind.Connection], _tree.Roots.Select(r => r.Kind));
        Assert.Equal(["a", "b"], _tree.Roots[0].Children.Select(c => c.Title));
        Assert.All(_tree.Roots[0].Children, c => Assert.Equal(ExplorerNodeKind.Connection, c.Kind));
    }

    [Fact]
    public void DisconnectedConnection_IsALeaf()
    {
        _list.Profiles.Add(Profile("p"));

        var node = _tree.Roots.Single();

        Assert.False(node.IsConnected);
        Assert.Empty(node.Children);
    }

    [Fact]
    public void Connect_AddsDatabaseSchemaAndTables_InPlace_AndExpandsTheConnection()
    {
        var profile = Profile("p", database: "ventes");
        _list.Profiles.Add(profile);
        var before = _tree.Roots.Single();

        var node = Connect(profile, Table("public", "users"), Table("public", "v_users", TableKind.View));

        Assert.Same(before, node);
        Assert.True(node.IsConnected);
        Assert.True(node.IsExpanded);
        var database = Assert.Single(node.Children);
        Assert.Equal((ExplorerNodeKind.Database, "ventes"), (database.Kind, database.Title));
        var schema = Assert.Single(database.Children);
        Assert.Equal((ExplorerNodeKind.Schema, "public"), (schema.Kind, schema.Title));
        Assert.Equal(["Tables", "Vues"], schema.Children.Select(c => c.Title));
        Assert.Equal(["users"], schema.Children[0].Children.Select(c => c.Title));
        Assert.Equal([ExplorerNodeKind.View], schema.Children[1].Children.Select(c => c.Kind));
        Assert.Equal(["Colonnes", "Index", "Clés étrangères"], schema.Children[0].Children[0].Children.Select(c => c.Title));
        Assert.All(Descendants(node), d => Assert.Same(node.Session, d.Session));
    }

    [Fact]
    public void Connect_DatabaseFallsBackToProfileName_AndSchemalessTablesSitUnderTheDatabase()
    {
        var profile = Profile("lite", database: null);
        _list.Profiles.Add(profile);

        var database = Connect(profile, Table(null, "t")).Children.Single();

        Assert.Equal("lite", database.Title);
        Assert.Equal(["Tables"], database.Children.Select(c => c.Title));
    }

    [Fact]
    public void Connect_SeveralSchemas_GivesOneSchemaNodeEach()
    {
        var profile = Profile("p");
        _list.Profiles.Add(profile);

        var database = Connect(profile, Table("a", "t1"), Table("b", "t2"), Table("a", "t3")).Children.Single();

        Assert.Equal(["a", "b"], database.Children.Select(c => c.Title));
        Assert.All(database.Children, c => Assert.Equal(ExplorerNodeKind.Schema, c.Kind));
        Assert.Equal(["t1", "t3"], database.Children[0].Children[0].Children.Select(c => c.Title));
    }

    [Fact]
    public void Disconnect_TurnsTheNodeBackIntoACollapsedLeaf()
    {
        var profile = Profile("p");
        _list.Profiles.Add(profile);
        var node = Connect(profile, Table("public", "users"));

        _sessions.RemoveAt(0);

        Assert.False(node.IsConnected);
        Assert.False(node.IsExpanded);
        Assert.Empty(node.Children);
    }

    [Fact]
    public void ProfilesChange_KeepsOpenSessionsAndExpansion()
    {
        var profile = Profile("p");
        _list.Profiles.Add(profile);
        Connect(profile, Table("public", "users"));
        _list.Profiles.Add(Profile("other"));

        var node = _tree.Roots.Single(r => r.Title == "p");

        Assert.True(node.IsConnected);
        Assert.True(node.IsExpanded);
        Assert.Single(node.Children);
    }

    [Fact]
    public void Columns_HaveTypeAndMarkers()
    {
        var profile = Profile("p");
        _list.Profiles.Add(profile);
        var node = Connect(profile, Table("public", "users"));

        var columns = Descendants(node).Single(n => n.Title == "Colonnes").Children;

        Assert.Equal("int PK", columns[0].Detail);
        Assert.Equal("text NULL", columns[1].Detail);
    }

    [Fact]
    public void SelectingANode_SelectsItsConnectionInTheList()
    {
        var profile = Profile("p");
        _list.Profiles.Add(profile);
        var node = Connect(profile, Table("public", "users"));

        _tree.SelectedNode = Descendants(node).First(n => n.Kind == ExplorerNodeKind.Table);

        Assert.Same(profile, _list.SelectedProfile);
    }

    [Fact]
    public void FilterText_HidesNonMatchingNodesAndKeepsAncestors()
    {
        var a = Profile("a");
        var b = Profile("b");
        _list.Profiles.Add(a);
        _list.Profiles.Add(b);
        var nodeA = Connect(a, Table("public", "users"));
        var nodeB = Connect(b, Table("public", "orders"));

        _tree.FilterText = "EMAIL";

        Assert.True(nodeA.IsVisible);
        var users = Descendants(nodeA).Single(n => n.Kind == ExplorerNodeKind.Table);
        Assert.Equal([false, true], Descendants(users).Single(n => n.Title == "Colonnes").Children.Select(c => c.IsVisible));
        Assert.True(Descendants(nodeA).Single(n => n.Kind == ExplorerNodeKind.Database).IsVisible);

        _tree.FilterText = "zzz";
        Assert.All(_tree.Roots, r => Assert.False(r.IsVisible));

        _tree.FilterText = "orders";
        Assert.False(nodeA.IsVisible);
        Assert.True(nodeB.IsVisible);

        _tree.FilterText = "";
        Assert.All(_tree.Roots, r => Assert.True(r.IsVisible));
    }

    [Fact]
    public async Task RefreshCommand_RebuildsOnlyTheSelectedConnection()
    {
        var a = Profile("a");
        var b = Profile("b");
        _list.Profiles.Add(a);
        _list.Profiles.Add(b);
        _sessions.Add(Session(a, new SchemaSnapshot(null, [Table("public", "users")]), new SchemaSnapshot(null, [Table("public", "users"), Table("public", "extra")])));
        _sessions.Add(Session(b, new SchemaSnapshot(null, [Table("public", "x")]), new SchemaSnapshot(null, [Table("public", "x"), Table("public", "y")])));
        var nodeA = _tree.Roots.Single(r => r.Title == "a");
        var nodeB = _tree.Roots.Single(r => r.Title == "b");
        _tree.SelectedNode = nodeA;

        await _tree.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, Descendants(nodeA).Count(d => d.Kind == ExplorerNodeKind.Table));
        Assert.Equal(1, Descendants(nodeB).Count(d => d.Kind == ExplorerNodeKind.Table));
    }

    [Fact]
    public void ActivateCommand_ConnectsDisconnectedConnections_AndOpensTablesAndViewsWithTheirSession()
    {
        var profile = Profile("p");
        _list.Profiles.Add(profile);
        var node = _tree.Roots.Single();
        var connect = new List<ConnectionProfile>();
        var opened = new List<(string, ConnectionSession)>();
        _tree.ConnectRequested += (_, p) => connect.Add(p);
        _tree.OpenTableRequested += (_, r) => opened.Add((r.Table.Name, r.Session));

        _tree.ActivateCommand.Execute(node);
        Connect(profile, Table("public", "users"), Table("public", "v", TableKind.View));
        _tree.ActivateCommand.Execute(node);
        foreach (var n in Descendants(node))
            _tree.ActivateCommand.Execute(n);

        Assert.Same(profile, Assert.Single(connect));
        Assert.Equal(["users", "v"], opened.Select(o => o.Item1));
        Assert.All(opened, o => Assert.Same(node.Session, o.Item2));
    }

    [Fact]
    public void DesignAndDropCommands_RaiseForTablesOnly()
    {
        var profile = Profile("p");
        _list.Profiles.Add(profile);
        var node = Connect(profile, Table("public", "users"), Table("public", "v", TableKind.View));
        var design = new List<string>();
        var drop = new List<string>();
        _tree.DesignTableRequested += (_, r) => design.Add(r.Table.Name);
        _tree.DropTableRequested += (_, r) => drop.Add(r.Table.Name);

        foreach (var n in Descendants(node))
        {
            _tree.DesignCommand.Execute(n);
            _tree.DropCommand.Execute(n);
        }

        Assert.Equal(["users"], design);
        Assert.Equal(["users"], drop);
    }

    [Theory]
    [InlineData(UiLanguage.French, "Colonnes", "Index", "Clés étrangères")]
    [InlineData(UiLanguage.English, "Columns", "Indexes", "Foreign keys")]
    public void TableFolders_AreInTheChosenLanguage(UiLanguage language, string columns, string indexes, string foreignKeys)
    {
        using var _ = UiText.Override(language);
        var profile = Profile("p");
        _list.Profiles.Add(profile);
        var node = Connect(profile, Table("public", "users"));

        var users = Descendants(node).Single(d => d.Title == "users");
        Assert.Equal([columns, indexes, foreignKeys], users.Children.Select(c => c.Title));
    }

    private static ExplorerNode Folder(ExplorerNode table, string title) => table.Children.Single(c => c.Title == title);

    [Fact]
    public void Counts_SchemasAndFolders_AreSetAndLeavesHaveNone()
    {
        var profile = Profile("p");
        _list.Profiles.Add(profile);
        var bare = new TableInfo("sales", "bare", TableKind.Table, [Id], [], []);
        var node = Connect(profile, Table("public", "users"), Table("public", "v", TableKind.View), Table("sales", "orders"), bare);
        var schemas = Descendants(node).Where(d => d.Kind == ExplorerNodeKind.Schema).ToDictionary(d => d.Title);

        Assert.Equal(2, schemas["public"].Count);
        Assert.Equal(2, schemas["sales"].Count);
        var users = Descendants(node).Single(d => d.Title == "users");
        Assert.Equal(2, Folder(users, "Colonnes").Count);
        Assert.Equal(1, Folder(users, "Index").Count);
        Assert.Equal(1, Folder(users, "Clés étrangères").Count);
        Assert.Equal(0, Folder(Descendants(node).Single(d => d.Title == "bare"), "Index").Count);
        Assert.All(Descendants(node).Where(d => d.Kind is ExplorerNodeKind.Table or ExplorerNodeKind.View or ExplorerNodeKind.Column), d => Assert.Null(d.Count));
    }

    [Fact]
    public void Trailing_PrefersDetail_ThenFormattedCount_ThenNull()
    {
        using var _ = UiText.Override(UiLanguage.English);
        Assert.Equal("d", new ExplorerNode("t", ExplorerNodeKind.Column, "d", count: 3).Trailing);
        Assert.Equal("1,208", new ExplorerNode("t", ExplorerNodeKind.Folder, count: 1208).Trailing);
        Assert.Null(new ExplorerNode("t", ExplorerNodeKind.Table).Trailing);
    }

    [Fact]
    public void FilterText_DoesNotChangeCounts()
    {
        var profile = Profile("p");
        _list.Profiles.Add(profile);
        var node = Connect(profile, Table("public", "users"), Table("public", "orders"));
        var schema = Descendants(node).Single(d => d.Kind == ExplorerNodeKind.Schema);

        _tree.FilterText = "users";

        Assert.Equal(2, schema.Count);
    }

    [Fact]
    public async Task RefreshCommand_RecomputesCounts()
    {
        var a = Profile("a");
        _list.Profiles.Add(a);
        _sessions.Add(Session(a, new SchemaSnapshot(null, [Table("public", "users")]), new SchemaSnapshot(null, [Table("public", "users"), Table("public", "extra")])));
        var node = _tree.Roots.Single();
        _tree.SelectedNode = node;

        await _tree.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, Descendants(node).Single(d => d.Kind == ExplorerNodeKind.Schema).Count);
    }

    private (ExplorerNode Connection, ConnectionSession Root, List<ConnectionSession> Opened) ConnectServer(ConnectionProfile profile)
    {
        _list.Profiles.Add(profile);
        var opened = new List<ConnectionSession>();
        ConnectionSession? root = null;
        var own = new SchemaSnapshot(null, [Table("public", "own")]);
        var remote = new SchemaSnapshot(null, [Table("public", "remote")]);
        root = new ConnectionSession(profile, new FakeConnection(own), own, null)
        {
            Databases = ["d", "other", "bad"],
            DatabaseOpener = async (name, _) =>
            {
                await Task.Yield();
                if (name == "bad")
                    throw new InvalidOperationException("permission denied");
                var session = new ConnectionSession(profile with { Database = name }, new FakeConnection(remote), remote, null) { Parent = root };
                opened.Add(session);
                return session;
            },
        };
        _sessions.Add(root);
        return (_tree.Roots.Single(), root, opened);
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
            await Task.Delay(10);
        Assert.True(condition());
    }

    [Fact]
    public void Connect_ListsOtherDatabasesAfterTheOwnOne_Collapsed_AndNotOpened()
    {
        var (connection, _, opened) = ConnectServer(Profile("p"));

        Assert.Equal(["d", "other", "bad"], connection.Children.Select(c => c.Title));
        Assert.All(connection.Children, c => Assert.Equal(ExplorerNodeKind.Database, c.Kind));
        Assert.True(connection.Children[0].IsExpanded);
        Assert.False(connection.Children[1].IsExpanded);
        Assert.Null(connection.Children[1].Session);
        Assert.Empty(opened);
    }

    [Fact]
    public void Populate_SortsTablesAndViewsAlphabetically_IgnoringCase()
    {
        var profile = Profile("p");
        _list.Profiles.Add(profile);
        var node = Connect(profile, Table("public", "zebra"), Table("public", "Beta"), Table("public", "alpha"));

        var folder = Descendants(node).First(d => d.Kind == ExplorerNodeKind.Folder && d.Children.Any(c => c.Kind == ExplorerNodeKind.Table));

        Assert.Equal(["alpha", "Beta", "zebra"], folder.Children.Select(c => c.Title));
    }

    [Fact]
    public async Task UnopenedDatabase_HasAChildSoTheTreeShowsAnExpander_AndActivatingItOpensIt()
    {
        var (connection, _, opened) = ConnectServer(Profile("p"));
        var other = connection.Children[1];

        Assert.NotEmpty(other.Children);
        _tree.ActivateCommand.Execute(other);
        await WaitAsync(() => other.Session is not null);

        Assert.Single(opened);
        Assert.DoesNotContain(other.Children, c => c.Title == "…");
        Assert.Contains(Descendants(other), n => n.Title == "remote");
    }

    [Fact]
    public async Task ExpandOtherDatabase_OpensDerivedSession_ShowsItsSchema_AndReusesItAfterRebuild()
    {
        var (connection, root, opened) = ConnectServer(Profile("p"));
        var other = connection.Children[1];

        other.IsExpanded = true;
        await WaitAsync(() => other.Session is not null);

        Assert.Same(root, other.Session!.Parent);
        Assert.Equal("other", other.Profile!.Database);
        Assert.Contains(Descendants(other), n => n is { Kind: ExplorerNodeKind.Table, Title: "remote" } && n.Session == other.Session);
        _tree.Reload(root);
        Assert.Same(other.Session, connection.Children[1].Session);
        Assert.Single(opened);
    }

    [Fact]
    public async Task ExpandOtherDatabase_Failure_ShowsTheMessageAndCollapses()
    {
        var (connection, _, _) = ConnectServer(Profile("p"));
        var bad = connection.Children[2];

        bad.IsExpanded = true;
        await WaitAsync(() => bad.ErrorText is not null);

        Assert.Equal("permission denied", bad.Trailing);
        Assert.False(bad.IsExpanded);
        Assert.Null(bad.Session);
    }

    [Fact]
    public async Task RefreshDatabaseCommand_RebuildsOnlyThatDatabase()
    {
        var (connection, _, _) = ConnectServer(Profile("p"));
        var other = connection.Children[1];
        other.IsExpanded = true;
        await WaitAsync(() => other.Session is not null);
        var own = connection.Children[0];
        var ownChildren = own.Children.ToList();

        await _tree.RefreshDatabaseCommand.ExecuteAsync(other);

        Assert.Same(other.Session, connection.Children[1].Session);
        Assert.Equal(ownChildren, own.Children);
        Assert.Contains(Descendants(connection.Children[1]), n => n.Title == "remote");
    }

    [Fact]
    public async Task RefreshCommand_OnDerivedNode_RefreshesOnlyThatSession()
    {
        var (connection, root, _) = ConnectServer(Profile("p"));
        var other = connection.Children[1];
        other.IsExpanded = true;
        await WaitAsync(() => other.Session is not null && Descendants(other).Any(n => n.Kind == ExplorerNodeKind.Table));
        _tree.SelectedNode = Descendants(other).First(n => n.Kind == ExplorerNodeKind.Table);

        await _tree.RefreshCommand.ExecuteAsync(null);

        Assert.Same(other.Session, connection.Children[1].Session);
        Assert.Contains(Descendants(connection.Children[1]), n => n.Title == "remote");
        Assert.Contains(Descendants(connection.Children[0]), n => n.Title == "own");
    }

    private sealed class MemoryFilters : ITableFilterStore
    {
        public Dictionary<Guid, TableFilter> Saved { get; } = [];

        public ValueTask<TableFilter?> LoadAsync(Guid profileId, CancellationToken ct) => ValueTask.FromResult(Saved.GetValueOrDefault(profileId));

        public ValueTask SaveAsync(Guid profileId, TableFilter filter, CancellationToken ct)
        {
            Saved[profileId] = filter;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FixedPicker(params string[] shown) : ITableVisibilityPicker
    {
        public IReadOnlyList<TableChoice>? Offered { get; private set; }

        public ValueTask<IReadOnlyList<string>?> PickVisibleAsync(string title, IReadOnlyList<TableChoice> choices)
        {
            Offered = choices;
            return ValueTask.FromResult<IReadOnlyList<string>?>(shown);
        }
    }

    private (ExplorerTreeViewModel Tree, ExplorerNode Database, MemoryFilters Filters, ConnectionProfile Profile) Filtered(MemoryFilters? filters = null)
    {
        filters ??= new MemoryFilters();
        var tree = new ExplorerTreeViewModel(_list, _sessions, filters);
        var profile = Profile("f");
        _list.Profiles.Add(profile);
        _sessions.Add(Session(profile, new SchemaSnapshot(null, [Table("public", "users"), Table("public", "logs"), Table("public", "v", TableKind.View)])));
        return (tree, tree.Roots.Single(n => n.Profile == profile).Children.Single(), filters, profile);
    }

    private static IEnumerable<string> TableTitles(ExplorerNode database) =>
        Descendants(database).Where(n => n.Kind is ExplorerNodeKind.Table or ExplorerNodeKind.View).Select(n => n.Title);

    [Fact]
    public async Task ChooseTables_HidesTheUnchecked_ShowsACount_AndSavesPerDatabase()
    {
        var (tree, database, filters, profile) = Filtered();
        var picker = new FixedPicker("public.users", "public.v");
        tree.Picker = picker;

        await tree.ChooseTablesCommand.ExecuteAsync(database);

        Assert.Equal(["public.logs", "public.users", "public.v"], picker.Offered!.Select(c => c.Key));
        Assert.Equal(["users", "v"], TableTitles(database));
        Assert.Equal(1, database.HiddenCount);
        Assert.Equal(UiText.FormatPlural("explorer.hiddenTables", 1), database.Trailing);
        Assert.Equal(["public.logs"], filters.Saved[profile.Id].Hidden["d"]);
    }

    [Fact]
    public void HideTable_ThenShowAll_RoundTrips()
    {
        var (tree, database, _, _) = Filtered();
        var users = Descendants(database).First(n => n.Title == "users");

        tree.HideTableCommand.Execute(users);
        Assert.DoesNotContain("users", TableTitles(database));

        tree.ShowAllTablesCommand.Execute(database);
        Assert.Contains("users", TableTitles(database));
        Assert.Equal(0, database.HiddenCount);
        Assert.Null(database.Trailing);
    }

    [Fact]
    public async Task SavedFilter_IsAppliedWhenTheConnectionOpens()
    {
        var saved = new MemoryFilters();
        var profile = Profile("g");
        saved.Saved[profile.Id] = new TableFilter(new Dictionary<string, IReadOnlyList<string>> { ["d"] = ["public.logs"] });
        var tree = new ExplorerTreeViewModel(_list, _sessions, saved);
        _list.Profiles.Add(profile);

        _sessions.Add(Session(profile, new SchemaSnapshot(null, [Table("public", "users"), Table("public", "logs")])));
        var database = tree.Roots.Single(n => n.Profile == profile).Children.Single();
        await WaitAsync(() => database.HiddenCount == 1);

        Assert.Equal(["users"], TableTitles(database));
    }

    [Fact]
    public async Task CancelledPicker_ChangesNothing()
    {
        var (tree, database, filters, _) = Filtered();
        tree.Picker = new CancelPicker();

        await tree.ChooseTablesCommand.ExecuteAsync(database);

        Assert.Empty(filters.Saved);
        Assert.Equal(3, TableTitles(database).Count());
    }

    private sealed class CancelPicker : ITableVisibilityPicker
    {
        public ValueTask<IReadOnlyList<string>?> PickVisibleAsync(string title, IReadOnlyList<TableChoice> choices) => ValueTask.FromResult<IReadOnlyList<string>?>(null);
    }

    private sealed class FakeStore : IProfileStore
    {
        public ValueTask<IReadOnlyList<ConnectionProfile>> ListAsync(CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<ConnectionProfile>>([]);
        public ValueTask SaveAsync(ConnectionProfile profile, string? password, string? sshSecret, CancellationToken ct) => ValueTask.CompletedTask;
        public ValueTask DeleteAsync(Guid id, CancellationToken ct) => ValueTask.CompletedTask;
        public ValueTask<string?> GetPasswordAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>(null);
        public ValueTask<string?> GetSshSecretAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>(null);
    }

    private sealed class FakeConnection(SchemaSnapshot next) : IDatabaseConnection
    {
        public string ServerVersion => "1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(next);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private ExplorerNode ConnectWithObjects(ConnectionProfile profile)
    {
        var table = new TableInfo("public", "person", TableKind.Table, [Id], [], []);
        var view = new TableInfo("public", "v", TableKind.View, [Id], [], [], null, "SELECT 1");
        var snapshot = new SchemaSnapshot("public", [table, view])
        {
            Routines = [new RoutineInfo("public", "f", RoutineKind.Function, "f(a int)", "int", "CREATE FUNCTION f ..."), new RoutineInfo("public", "p", RoutineKind.Procedure, "p()", null, "CREATE PROCEDURE p ...")],
            Triggers = [new TriggerInfo("public", "person", "tr", "BEFORE", "INSERT", "CREATE TRIGGER tr ...")],
        };
        _list.Profiles.Add(profile);
        _sessions.Add(Session(profile, snapshot));
        return _tree.Roots.Single(n => n.Profile == profile);
    }

    [Fact]
    public void Populate_RoutinesAndTriggers_AppearInFoldersWithCounts()
    {
        var schema = ConnectWithObjects(Profile("c")).Children.Single().Children.Single();
        Assert.Equal([UiText.Get("explorer.tables"), UiText.Get("explorer.views"), UiText.Get("explorer.routines"), UiText.Get("explorer.triggers")], schema.Children.Select(c => c.Title));
        Assert.Equal([1, 1, 2, 1], schema.Children.Select(c => c.Count));
        Assert.Equal([ExplorerNodeKind.Function, ExplorerNodeKind.Procedure], schema.Children[2].Children.Select(c => c.Kind));
        Assert.Equal(ExplorerNodeKind.Trigger, schema.Children[3].Children.Single().Kind);
    }

    [Fact]
    public void Populate_NoRoutinesOrTriggers_ShowsNoEmptyFolder()
    {
        var profile = Profile("c");
        _list.Profiles.Add(profile);
        var node = Connect(profile, Table("public", "t"));
        Assert.DoesNotContain(Descendants(node), n => n.Title == UiText.Get("explorer.routines") || n.Title == UiText.Get("explorer.triggers"));
    }

    [Fact]
    public void OpenDefinition_ObjectNodes_RaiseScriptWithDefinition()
    {
        var connection = ConnectWithObjects(Profile("c"));
        var scripts = new List<string>();
        _tree.ScriptRequested += (_, r) => scripts.Add(r.Text);
        foreach (var kind in new[] { ExplorerNodeKind.View, ExplorerNodeKind.Function, ExplorerNodeKind.Trigger, ExplorerNodeKind.Table })
            _tree.OpenDefinitionCommand.Execute(Descendants(connection).First(n => n.Kind == kind));
        Assert.Equal(["CREATE VIEW \"public\".\"v\" AS\nSELECT 1", "CREATE FUNCTION f ...", "CREATE TRIGGER tr ..."], scripts);
    }

    [Fact]
    public void ScriptCreateTable_Table_RaisesCreateTableScript()
    {
        var connection = ConnectWithObjects(Profile("c"));
        string? text = null;
        _tree.ScriptRequested += (_, r) => text = r.Text;
        _tree.ScriptCreateTableCommand.Execute(Descendants(connection).First(n => n.Kind == ExplorerNodeKind.Table));
        Assert.Contains("CREATE TABLE", text);
    }
}
