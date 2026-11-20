using System.Collections.ObjectModel;
using Squill.Core;
using Squill.Data;
using Squill.Platform.Profiles;
using Squill.Platform.Storage;
using Squill.Presentation.Connections;
using Squill.Presentation.Explorer;
using Squill.Presentation.Localization;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.Presentation.Tests.Explorer;

public sealed class ExplorerMultiSelectionTests
{
    private static readonly ColumnDefinition Id = new("id", "int", false, true, null);

    private readonly ConnectionListViewModel _list = new(new FakeStore(), new ProviderRegistry(), new FakeDialogService());
    private readonly ObservableCollection<ConnectionSession> _sessions = [];
    private readonly ExplorerTreeViewModel _tree;
    private readonly List<TablesRequest> _tablesRequests = [];
    private readonly List<TableRequest> _tableRequests = [];

    public ExplorerMultiSelectionTests()
    {
        _tree = new ExplorerTreeViewModel(_list, _sessions);
        _tree.DropTablesRequested += (_, r) => _tablesRequests.Add(r);
        _tree.DropTableRequested += (_, r) => _tableRequests.Add(r);
    }

    private static TableInfo Table(string name, TableKind kind = TableKind.Table) => new(null, name, kind, [Id], [], []);

    private static ConnectionProfile Profile(string name) =>
        new(Guid.NewGuid(), name, DatabaseDialect.PostgreSql, "h", 1, "d", "u", null, null, ConnectionEnvironment.None, null);

    private ConnectionSession Connect(string name, params TableInfo[] tables)
    {
        var profile = Profile(name);
        _list.Profiles.Add(profile);
        var schema = new SchemaSnapshot(null, tables);
        var session = new ConnectionSession(profile, new FakeConnection(schema), schema, null);
        _sessions.Add(session);
        return session;
    }

    private static IEnumerable<ExplorerNode> Walk(IEnumerable<ExplorerNode> nodes) =>
        nodes.SelectMany(n => Walk(n.Children).Prepend(n));

    private ExplorerNode Find(string name, ExplorerNodeKind kind, ConnectionSession? session = null) =>
        Walk(_tree.Roots).Single(n => n.Kind == kind && n.Title == name && (session is null || n.Session == session));

    [Fact]
    public void Two_tables_of_one_session_drop_together_in_tree_order()
    {
        Connect("p", Table("b"), Table("a"));
        _tree.SetSelectedNodes([Find("b", ExplorerNodeKind.Table), Find("a", ExplorerNodeKind.Table)]);

        Assert.Equal(2, _tree.SelectedTableCount);
        Assert.True(_tree.DropSelectedTablesCommand.CanExecute(null));
        _tree.DropSelectedTablesCommand.Execute(null);

        var request = Assert.Single(_tablesRequests);
        Assert.Equal(["a", "b"], request.Tables.Select(t => t.Name));
    }

    [Fact]
    public void A_filter_that_hides_selected_tables_removes_them_from_the_drop()
    {
        Connect("p", Table("alpha"), Table("beta"));
        _tree.SetSelectedNodes([Find("alpha", ExplorerNodeKind.Table), Find("beta", ExplorerNodeKind.Table)]);

        _tree.FilterText = "alpha";
        _tree.DropSelectedTablesCommand.Execute(null);

        var request = Assert.Single(_tablesRequests);
        Assert.Equal(["alpha"], request.Tables.Select(t => t.Name));
    }

    [Fact]
    public void A_table_and_a_view_disable_the_command()
    {
        Connect("p", Table("a"), Table("v", TableKind.View));
        _tree.SetSelectedNodes([Find("a", ExplorerNodeKind.Table), Find("v", ExplorerNodeKind.View)]);

        Assert.False(_tree.DropSelectedTablesCommand.CanExecute(null));
    }

    [Fact]
    public void Tables_of_two_sessions_disable_the_command()
    {
        var one = Connect("p1", Table("a"));
        var two = Connect("p2", Table("b"));
        _tree.SetSelectedNodes([Find("a", ExplorerNodeKind.Table, one), Find("b", ExplorerNodeKind.Table, two)]);

        Assert.False(_tree.DropSelectedTablesCommand.CanExecute(null));
    }

    [Fact]
    public void An_empty_selection_disables_the_command()
    {
        Connect("p", Table("a"));
        _tree.SetSelectedNodes([]);

        Assert.False(_tree.DropSelectedTablesCommand.CanExecute(null));
        Assert.Equal(0, _tree.SelectedTableCount);
    }

    [Fact]
    public void Drop_on_a_node_inside_the_selection_drops_all_of_them()
    {
        Connect("p", Table("a"), Table("b"), Table("c"));
        var nodes = new[] { Find("a", ExplorerNodeKind.Table), Find("b", ExplorerNodeKind.Table), Find("c", ExplorerNodeKind.Table) };
        _tree.SetSelectedNodes(nodes);

        _tree.DropCommand.Execute(nodes[1]);

        Assert.Equal(3, Assert.Single(_tablesRequests).Tables.Count);
        Assert.Empty(_tableRequests);
    }

    [Fact]
    public void Drop_on_a_node_outside_the_selection_drops_only_that_table()
    {
        Connect("p", Table("a"), Table("b"), Table("c"));
        _tree.SetSelectedNodes([Find("a", ExplorerNodeKind.Table), Find("b", ExplorerNodeKind.Table)]);

        _tree.DropCommand.Execute(Find("c", ExplorerNodeKind.Table));

        Assert.Equal("c", Assert.Single(_tableRequests).Table.Name);
        Assert.Empty(_tablesRequests);
    }

    [Fact]
    public void DropMenuText_is_singular_or_plural_in_both_languages()
    {
        Connect("p", Table("a"), Table("b"));
        var a = Find("a", ExplorerNodeKind.Table);
        var b = Find("b", ExplorerNodeKind.Table);
        _tree.SetSelectedNodes([a]);
        using (UiText.Override(UiLanguage.English))
            Assert.Equal("Drop table...", _tree.DropMenuText(a));
        _tree.SetSelectedNodes([a, b]);
        using (UiText.Override(UiLanguage.English))
            Assert.Equal("Drop 2 tables", _tree.DropMenuText(a));
        using (UiText.Override(UiLanguage.French))
            Assert.Equal("Supprimer 2 tables", _tree.DropMenuText(b));
    }

    [Fact]
    public void CanDropMenu_is_false_for_a_mixed_selection_containing_the_node()
    {
        Connect("p", Table("a"), Table("v", TableKind.View));
        var a = Find("a", ExplorerNodeKind.Table);
        _tree.SetSelectedNodes([a, Find("v", ExplorerNodeKind.View)]);

        Assert.False(_tree.CanDropMenu(a));
        Assert.False(_tree.CanDropMenu(null));
    }

    [Fact]
    public void A_node_removed_by_a_reload_does_not_stay_selected()
    {
        var session = Connect("p", Table("a"), Table("b"));
        var a = Find("a", ExplorerNodeKind.Table);
        var b = Find("b", ExplorerNodeKind.Table);

        _tree.Reload(session);
        _tree.SetSelectedNodes([a, b]);

        Assert.Empty(_tree.SelectedNodes);
        Assert.False(_tree.DropSelectedTablesCommand.CanExecute(null));
    }

    [Fact]
    public void SetSelectedNodes_raises_the_change_notifications_and_keeps_SelectedNode()
    {
        Connect("p", Table("a"), Table("b"));
        var changed = new List<string?>();
        _tree.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var canChanged = 0;
        _tree.DropSelectedTablesCommand.CanExecuteChanged += (_, _) => canChanged++;

        _tree.SetSelectedNodes([Find("a", ExplorerNodeKind.Table), Find("b", ExplorerNodeKind.Table)]);

        Assert.Contains(nameof(ExplorerTreeViewModel.SelectedNodes), changed);
        Assert.Contains(nameof(ExplorerTreeViewModel.SelectedTableCount), changed);
        Assert.True(canChanged > 0);
        Assert.Null(_tree.SelectedNode);
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
}
