using Squill.Core;
using Squill.Sql.Ddl;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class TableDropPlanTests
{
    private static ForeignKeyInfo Fk(string? schema, string table) => new("fk", ["x"], schema, table, ["id"]);

    private static TableInfo T(string name, string? schema = "public", TableKind kind = TableKind.Table, params ForeignKeyInfo[] fks) =>
        new(schema, name, kind, [], [], fks);

    private static SchemaSnapshot Snap(params TableInfo[] tables) => new("public", tables);

    private static string[] Names(IEnumerable<TableInfo> tables) => [.. tables.Select(t => t.Name)];

    [Fact]
    public void Build_NoForeignKeys_KeepsSelectionOrder()
    {
        var a = T("a"); var b = T("b"); var c = T("c");
        var plan = TableDropPlan.Build([c, a, b], Snap(a, b, c));
        Assert.Equal(["c", "a", "b"], Names(plan.Order));
        Assert.Empty(plan.Blockers);
        Assert.False(plan.HasCycle);
    }

    [Fact]
    public void Build_ReferencingTable_GoesBeforeReferenced()
    {
        var clients = T("clients");
        var commandes = T("commandes", "public", TableKind.Table, Fk("public", "clients"));
        var plan = TableDropPlan.Build([clients, commandes], Snap(clients, commandes));
        Assert.Equal(["commandes", "clients"], Names(plan.Order));
    }

    [Fact]
    public void Build_Chain_OrdersFromLeafToRoot()
    {
        var a = T("a");
        var b = T("b", "public", TableKind.Table, Fk("public", "a"));
        var c = T("c", "public", TableKind.Table, Fk("public", "b"));
        var plan = TableDropPlan.Build([a, b, c], Snap(a, b, c));
        Assert.Equal(["c", "b", "a"], Names(plan.Order));
    }

    [Fact]
    public void Build_Diamond_IsStable()
    {
        var a = T("a");
        var b = T("b", "public", TableKind.Table, Fk("public", "a"));
        var c = T("c", "public", TableKind.Table, Fk("public", "a"));
        var d = T("d", "public", TableKind.Table, Fk("public", "b"), Fk("public", "c"));
        var plan = TableDropPlan.Build([a, b, c, d], Snap(a, b, c, d));
        Assert.Equal(["d", "b", "c", "a"], Names(plan.Order));
        Assert.False(plan.HasCycle);
    }

    [Fact]
    public void Build_Cycle_SetsFlagAndKeepsSelectionOrder()
    {
        var a = T("a", "public", TableKind.Table, Fk("public", "b"));
        var b = T("b", "public", TableKind.Table, Fk("public", "a"));
        var plan = TableDropPlan.Build([a, b], Snap(a, b));
        Assert.True(plan.HasCycle);
        Assert.Equal(["a", "b"], Names(plan.Order));
    }

    [Fact]
    public void Build_ForeignKeyFromOutsideSelection_IsABlocker()
    {
        var clients = T("clients");
        var commandes = T("commandes", "public", TableKind.Table, Fk("public", "clients"));
        var plan = TableDropPlan.Build([clients], Snap(clients, commandes));
        var blocker = Assert.Single(plan.Blockers);
        Assert.Equal("commandes", blocker.Referencing.Name);
        Assert.Equal("clients", blocker.Referenced.Name);
        Assert.Equal(["clients"], Names(plan.Order));
    }

    [Fact]
    public void Build_SelfReference_IsIgnored()
    {
        var emp = T("emp", "public", TableKind.Table, Fk("public", "emp"));
        var plan = TableDropPlan.Build([emp], Snap(emp));
        Assert.Empty(plan.Blockers);
        Assert.False(plan.HasCycle);
        Assert.Equal(["emp"], Names(plan.Order));
    }

    [Fact]
    public void Build_NullReferencedSchema_ResolvesToOwnerSchema()
    {
        var clients = T("clients", "shop");
        var commandes = T("commandes", "shop", TableKind.Table, Fk(null, "clients"));
        var other = T("clients", "other");
        var plan = TableDropPlan.Build([clients, commandes], new SchemaSnapshot("shop", [clients, commandes, other]));
        Assert.Equal(["commandes", "clients"], Names(plan.Order));
        Assert.Empty(plan.Blockers);
    }

    [Fact]
    public void Build_NamesDifferingByCase_Match()
    {
        var clients = T("Clients", "PUBLIC");
        var commandes = T("commandes", "public", TableKind.Table, Fk("public", "CLIENTS"));
        var plan = TableDropPlan.Build([clients, commandes], Snap(clients, commandes));
        Assert.Equal(["commandes", "Clients"], Names(plan.Order));
    }

    [Fact]
    public void Build_ViewInSelection_Throws()
    {
        var v = T("v", "public", TableKind.View);
        Assert.Throws<ArgumentException>(() => TableDropPlan.Build([v], Snap(v)));
    }

    [Fact]
    public void Build_Duplicates_AreRemoved()
    {
        var a = T("a");
        var plan = TableDropPlan.Build([a, a with { }], Snap(a));
        Assert.Equal(["a"], Names(plan.Order));
    }
}
