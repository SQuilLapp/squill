using System.Collections.ObjectModel;
using Squill.Core;
using Squill.Data;
using Squill.Platform.Profiles;
using Squill.Presentation.Connections;
using Squill.Presentation.Explorer;
using Squill.Presentation.Sessions;
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
        Assert.Equal(["Columns", "Indexes", "Foreign keys"], schema.Children[0].Children[0].Children.Select(c => c.Title));
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

        var columns = Descendants(node).Single(n => n.Title == "Columns").Children;

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
        Assert.Equal([false, true], Descendants(users).Single(n => n.Title == "Columns").Children.Select(c => c.IsVisible));
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

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(next);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
