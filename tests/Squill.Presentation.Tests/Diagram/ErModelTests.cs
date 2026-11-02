using Squill.Core;
using Squill.Presentation.Diagram;
using Xunit;

namespace Squill.Presentation.Tests.Diagram;

public sealed class ErModelTests
{
    private static ColumnDefinition Col(string n, bool pk = false, bool nullable = false) => new(n, "int", nullable, pk, null);

    private static TableInfo T(string? schema, string name, ColumnDefinition[] cols, ForeignKeyInfo[]? fks = null,
        IndexInfo[]? idx = null, TableKind kind = TableKind.Table) =>
        new(schema, name, kind, cols, idx ?? [], fks ?? []);

    private static ForeignKeyInfo Fk(string[] cols, string? refSchema, string refTable, string[]? refCols = null) =>
        new("fk_" + refTable, cols, refSchema, refTable, refCols ?? ["id"]);

    private static SchemaSnapshot Snap(params TableInfo[] t) => new("dbo", t);

    [Fact]
    public void Build_SingleTable_OneEntityKeyedByName()
    {
        var m = ErModel.Build(Snap(T(null, "a", [Col("id", pk: true), Col("x", nullable: true)])));
        var e = Assert.Single(m.Entities);
        Assert.Equal("a", e.Key);
        Assert.True(e.Columns[0].IsPrimaryKey);
        Assert.True(e.Columns[1].IsNullable);
        Assert.Empty(m.Relations);
    }

    [Fact]
    public void Build_ForeignKey_ManyToOneRequiredOrOptional()
    {
        var m = ErModel.Build(Snap(
            T("dbo", "p", [Col("id", pk: true)]),
            T("dbo", "c", [Col("id", pk: true), Col("p_id"), Col("q_id", nullable: true)],
                [Fk(["p_id"], null, "p"), new("fk_q", ["q_id"], "dbo", "p", ["id"])])));
        Assert.Equal(2, m.Relations.Count);
        var req = m.Relations.Single(r => r.Name == "fk_p");
        Assert.Equal(("dbo.c", "dbo.p", ErCardinality.ManyToOne, false), (req.FromKey, req.ToKey, req.Cardinality, req.IsOptional));
        Assert.True(m.Relations.Single(r => r.Name == "fk_q").IsOptional);
        Assert.True(m.Find("DBO.C")!.Columns[1].IsForeignKey);
        Assert.False(m.Find("dbo.c")!.Columns[0].IsForeignKey);
    }

    [Fact]
    public void Build_UniqueIndexOrPrimaryKey_IsOneToOne()
    {
        var m = ErModel.Build(Snap(
            T("dbo", "p", [Col("id", pk: true)]),
            T("dbo", "u", [Col("id", pk: true), Col("p_id")], [Fk(["p_id"], null, "p")], [new IndexInfo("ux", true, ["p_id"])]),
            T("dbo", "k", [Col("p_id", pk: true)], [Fk(["p_id"], null, "p")]),
            T("dbo", "n", [Col("id", pk: true), Col("p_id")], [Fk(["p_id"], null, "p")], [new IndexInfo("ix", false, ["p_id"])])));
        var byFrom = m.Relations.ToDictionary(r => r.FromKey, r => r.Cardinality);
        Assert.Equal(ErCardinality.OneToOne, byFrom["dbo.u"]);
        Assert.Equal(ErCardinality.OneToOne, byFrom["dbo.k"]);
        Assert.Equal(ErCardinality.ManyToOne, byFrom["dbo.n"]);
    }

    [Fact]
    public void Build_CompositeForeignKey_KeepsColumnsAndPartialPkIsManyToOne()
    {
        var m = ErModel.Build(Snap(
            T("dbo", "p", [Col("a", pk: true), Col("b", pk: true)]),
            T("dbo", "c", [Col("a", pk: true), Col("b"), Col("id")], [Fk(["a", "b"], null, "p", ["a", "b"])])));
        var r = Assert.Single(m.Relations);
        Assert.Equal(["a", "b"], r.FromColumns);
        Assert.Equal(["a", "b"], r.ToColumns);
        Assert.Equal(ErCardinality.ManyToOne, r.Cardinality);
    }

    [Fact]
    public void Build_CrossSchemaAndSelfReference_AreResolved()
    {
        var m = ErModel.Build(Snap(
            T("s1", "p", [Col("id", pk: true)]),
            T("s2", "c", [Col("id", pk: true), Col("p_id"), Col("parent_id", nullable: true)],
                [Fk(["p_id"], "s1", "p"), new("fk_self", ["parent_id"], null, "c", ["id"])])));
        Assert.Contains(m.Relations, r => r.FromKey == "s2.c" && r.ToKey == "s1.p");
        Assert.Contains(m.Relations, r => r.FromKey == "s2.c" && r.ToKey == "s2.c");
        Assert.Equal(["s1.p", "s2.c"], m.Entities.Select(e => e.Key));
    }

    [Fact]
    public void Build_FilterAndUnresolved_CountsMissingTargets()
    {
        var snap = Snap(
            T("dbo", "p", [Col("id", pk: true)]),
            T("dbo", "c", [Col("id", pk: true), Col("p_id")], [Fk(["p_id"], null, "p"), Fk(["p_id"], null, "ghost")]));
        var all = ErModel.Build(snap);
        Assert.Single(all.Relations);
        Assert.Equal(1, all.UnresolvedRelations);

        var only = ErModel.Build(snap, new ErFilter(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "DBO.C" }));
        Assert.Equal("dbo.c", Assert.Single(only.Entities).Key);
        Assert.Empty(only.Relations);
        Assert.Equal(2, only.UnresolvedRelations);
    }

    [Fact]
    public void Build_Views_SkippedUnlessIncludedAndNeverHaveRelations()
    {
        var snap = Snap(
            T("dbo", "p", [Col("id", pk: true)]),
            T("dbo", "v", [Col("id")], [Fk(["id"], null, "p")], kind: TableKind.View));
        Assert.Single(ErModel.Build(snap).Entities);
        var m = ErModel.Build(snap, new ErFilter(null, IncludeViews: true));
        Assert.Equal(2, m.Entities.Count);
        Assert.Empty(m.Relations);
        Assert.Equal(0, m.UnresolvedRelations);
    }

    [Fact]
    public void Neighborhood_Depths()
    {
        var m = ErModel.Build(Snap(
            T("dbo", "a", [Col("id", pk: true)]),
            T("dbo", "b", [Col("id", pk: true), Col("a_id")], [Fk(["a_id"], null, "a")]),
            T("dbo", "c", [Col("id", pk: true), Col("b_id")], [Fk(["b_id"], null, "b")])));
        Assert.Equal(["dbo.a"], ErModel.Neighborhood(m, "dbo.a", 0).Order());
        Assert.Equal(["dbo.a", "dbo.b"], ErModel.Neighborhood(m, "dbo.a", 1).Order());
        Assert.Equal(["dbo.a", "dbo.b", "dbo.c"], ErModel.Neighborhood(m, "dbo.a", 2).Order());
        Assert.Equal(["dbo.b", "dbo.c"], ErModel.Neighborhood(m, "DBO.C", 1).Order());
    }
}
