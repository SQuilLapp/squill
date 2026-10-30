using Squill.Core;
using Squill.Data;
using Squill.Presentation.Explorer;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.Presentation.Tests.Explorer;

public sealed class ObjectExplorerViewModelTests
{
    private static readonly ColumnDefinition Id = new("id", "int", false, true, null);
    private static readonly ColumnDefinition Email = new("email", "text", true, false, null);

    private static TableInfo Table(string? schema, string name, TableKind kind = TableKind.Table) =>
        new(schema, name, kind, [Id, Email], [new IndexInfo("ix_email", true, ["email"])],
            [new ForeignKeyInfo("fk_org", ["id"], "public", "orgs", ["id"])]);

    private static ObjectExplorerViewModel Create(FakeConnection connection, SchemaSnapshot schema) =>
        new(new ConnectionSession(
            new ConnectionProfile(Guid.NewGuid(), "p", DatabaseDialect.PostgreSql, "h", 1, "d", "u", null, null),
            connection, schema, null));

    private static ObjectExplorerViewModel Create(params TableInfo[] tables) =>
        Create(new FakeConnection(SchemaSnapshot.Empty), new SchemaSnapshot(null, tables));

    [Fact]
    public void Roots_SingleSchema_AreTables()
    {
        var vm = Create(Table("public", "users"), Table("public", "v_users", TableKind.View));

        Assert.Equal(["users", "v_users"], vm.Roots.Select(r => r.Title));
        Assert.Equal([ExplorerNodeKind.Table, ExplorerNodeKind.View], vm.Roots.Select(r => r.Kind));
        Assert.Equal(["Columns", "Indexes", "Foreign keys"], vm.Roots[0].Children.Select(c => c.Title));
    }

    [Fact]
    public void Roots_MultipleSchemas_AreSchemaNodes()
    {
        var vm = Create(Table("a", "t1"), Table("b", "t2"), Table("a", "t3"));

        Assert.Equal(["a", "b"], vm.Roots.Select(r => r.Title));
        Assert.All(vm.Roots, r => Assert.Equal(ExplorerNodeKind.Schema, r.Kind));
        Assert.Equal(["t1", "t3"], vm.Roots[0].Children.Select(c => c.Title));
    }

    [Fact]
    public void Columns_HaveTypeAndMarkers()
    {
        var columns = Create(Table("public", "users")).Roots[0].Children[0].Children;

        Assert.Equal("int PK", columns[0].Detail);
        Assert.Equal("text NULL", columns[1].Detail);
    }

    [Fact]
    public void FilterText_HidesNonMatchingLeavesAndKeepsAncestors()
    {
        var vm = Create(Table("a", "users"), Table("b", "orders"));

        vm.FilterText = "EMAIL";

        var users = vm.Roots[0].Children[0];
        Assert.True(vm.Roots[0].IsVisible);
        Assert.True(users.IsVisible);
        Assert.True(users.Children[0].IsVisible);
        Assert.Equal([false, true], users.Children[0].Children.Select(c => c.IsVisible));

        vm.FilterText = "zzz";
        Assert.All(vm.Roots, r => Assert.False(r.IsVisible));

        vm.FilterText = "";
        Assert.All(vm.Roots, r => Assert.True(r.IsVisible));
    }

    [Fact]
    public async Task RefreshCommand_RebuildsFromNewSchema()
    {
        var connection = new FakeConnection(new SchemaSnapshot(null, [Table("public", "users"), Table("public", "extra")]));
        var vm = Create(connection, new SchemaSnapshot(null, [Table("public", "users")]));
        Assert.Single(vm.Roots);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(["users", "extra"], vm.Roots.Select(r => r.Title));
    }

    [Fact]
    public void ActivateCommand_RaisesOnlyForTablesAndViews()
    {
        var vm = Create(Table("public", "users"), Table("public", "v", TableKind.View));
        var opened = new List<string>();
        vm.OpenTableRequested += (_, t) => opened.Add(t.Name);

        vm.ActivateCommand.Execute(vm.Roots[0]);
        vm.ActivateCommand.Execute(vm.Roots[1]);
        vm.ActivateCommand.Execute(vm.Roots[0].Children[0]);

        Assert.Equal(["users", "v"], opened);
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
