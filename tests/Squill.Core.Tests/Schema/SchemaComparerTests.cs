using System.Diagnostics;
using Squill.Core.Schema.Compare;
using Xunit;

namespace Squill.Core.Tests.Schema;

public sealed class SchemaComparerTests
{
    private static ColumnDefinition Col(string name, string type = "int", bool nullable = true, string? def = null, bool pk = false) =>
        new(name, type, nullable, pk, def);

    private static TableInfo Tbl(string name, IReadOnlyList<ColumnDefinition>? cols = null, IReadOnlyList<IndexInfo>? idx = null, IReadOnlyList<ForeignKeyInfo>? fks = null) =>
        new("public", name, TableKind.Table, cols ?? [Col("id", pk: true)], idx ?? [], fks ?? []);

    private static TableInfo View(string name, string? def) => new("public", name, TableKind.View, [], [], [], null, def);

    private static SchemaSnapshot Snap(params TableInfo[] tables) => new("public", tables);

    private static SchemaComparison Cmp(SchemaSnapshot s, SchemaSnapshot t) => SchemaComparer.Compare(s, t);

    private static ObjectDifference Single(SchemaComparison c) => Assert.Single(c.Differences);

    [Fact]
    public void Compare_IdenticalSnapshots_NoDifferences()
    {
        var s = Snap(Tbl("a"), View("v", "select 1"));
        Assert.Empty(Cmp(s, s).Differences);
    }

    [Fact]
    public void Compare_TableOnlyInSource_Added()
    {
        var d = Single(Cmp(Snap(Tbl("a")), Snap()));
        Assert.Equal((ObjectCategory.Table, "a", DifferenceKind.Added), (d.Category, d.Name, d.Kind));
    }

    [Fact]
    public void Compare_TableOnlyInTarget_Removed()
    {
        var c = Cmp(Snap(), Snap(Tbl("a")));
        Assert.Equal(DifferenceKind.Removed, Single(c).Kind);
        Assert.Equal(1, c.Count(DifferenceKind.Removed));
    }

    [Fact]
    public void Compare_ColumnAddedAndRemoved_Details()
    {
        var s = Snap(Tbl("a", [Col("id", pk: true), Col("x")]));
        var t = Snap(Tbl("a", [Col("id", pk: true), Col("y")]));
        var d = Single(Cmp(s, t));
        Assert.Equal(DifferenceKind.Modified, d.Kind);
        Assert.Equal([DetailKind.ColumnAdded, DetailKind.ColumnRemoved], d.Details.Select(x => x.Kind));
    }

    [Theory]
    [InlineData("varchar(10)", true, null)]
    [InlineData("int", false, null)]
    [InlineData("int", true, "0")]
    public void Compare_ColumnTypeNullabilityOrDefault_Changed(string type, bool nullable, string? def)
    {
        var s = Snap(Tbl("a", [Col("id", pk: true), Col("x", type, nullable, def)]));
        var t = Snap(Tbl("a", [Col("id", pk: true), Col("x")]));
        var detail = Assert.Single(Single(Cmp(s, t)).Details);
        Assert.Equal((DetailKind.ColumnChanged, "x"), (detail.Kind, detail.Subject));
    }

    [Fact]
    public void Compare_ColumnOrderOnly_NotReported()
    {
        var s = Snap(Tbl("a", [Col("x"), Col("y")]));
        var t = Snap(Tbl("a", [Col("y"), Col("x")]));
        Assert.Empty(Cmp(s, t).Differences);
    }

    [Fact]
    public void Compare_PrimaryKeyChange_Reported()
    {
        var s = Snap(Tbl("a", [Col("id", pk: true), Col("x")]));
        var t = Snap(Tbl("a", [Col("id"), Col("x", pk: true)]));
        Assert.Contains(Single(Cmp(s, t)).Details, d => d.Kind == DetailKind.PrimaryKeyChanged);
    }

    [Fact]
    public void Compare_ForeignKeyAddedRemovedChanged()
    {
        ForeignKeyInfo Fk(string n, string refTable, ReferentialAction del = ReferentialAction.NoAction) => new(n, ["id"], "public", refTable, ["id"], del);
        var s = Snap(Tbl("a", fks: [Fk("f1", "b"), Fk("f2", "b", ReferentialAction.Cascade), Fk("f3", "b")]));
        var t = Snap(Tbl("a", fks: [Fk("f2", "b"), Fk("f3", "b"), Fk("f4", "b")]));
        var kinds = Single(Cmp(s, t)).Details.ToDictionary(d => d.Subject, d => d.Kind);
        Assert.Equal(DetailKind.ForeignKeyAdded, kinds["f1"]);
        Assert.Equal(DetailKind.ForeignKeyChanged, kinds["f2"]);
        Assert.False(kinds.ContainsKey("f3"));
        Assert.Equal(DetailKind.ForeignKeyRemoved, kinds["f4"]);
    }

    [Fact]
    public void Compare_IndexAdded_ReportedOnTableAndAsIndex()
    {
        var s = Snap(Tbl("a", idx: [new IndexInfo("ix", false, ["id"])]));
        var c = Cmp(s, Snap(Tbl("a")));
        Assert.Equal([ObjectCategory.Table, ObjectCategory.Index], c.Differences.Select(d => d.Category));
        var ix = c.Differences[1];
        Assert.Equal(("a.ix", DifferenceKind.Added), (ix.Name, ix.Kind));
    }

    [Fact]
    public void Compare_IndexUniquenessChanged_ModifiedIndex()
    {
        var s = Snap(Tbl("a", idx: [new IndexInfo("ix", true, ["id"])]));
        var t = Snap(Tbl("a", idx: [new IndexInfo("ix", false, ["id"])]));
        Assert.Equal(DifferenceKind.Modified, Cmp(s, t).Differences.Single(d => d.Category == ObjectCategory.Index).Kind);
    }

    [Fact]
    public void Compare_ViewDefinitionWhitespaceOnly_NotReported()
    {
        Assert.Empty(Cmp(Snap(View("v", "select  1\n from t")), Snap(View("v", " select 1 from   t "))).Differences);
    }

    [Fact]
    public void Compare_ViewDefinitionChanged_Reported()
    {
        var d = Single(Cmp(Snap(View("v", "select 1")), Snap(View("v", "select 2"))));
        Assert.Equal((ObjectCategory.View, DetailKind.DefinitionChanged), (d.Category, d.Details[0].Kind));
    }

    [Fact]
    public void Compare_NullDefinitions_BothNullIgnoredOneNullUnknown()
    {
        Assert.Empty(Cmp(Snap(View("v", null)), Snap(View("v", null))).Differences);
        var d = Single(Cmp(Snap(View("v", "select 1")), Snap(View("v", null))));
        Assert.Equal(DetailKind.DefinitionUnknown, d.Details[0].Kind);
    }

    [Fact]
    public void Compare_RoutineChanged_AddedRemoved()
    {
        RoutineInfo R(string sig, string? def) => new("public", "f", RoutineKind.Function, sig, "int", def);
        var s = new SchemaSnapshot("public", []) { Routines = [R("f(int)", "a"), R("f(text)", "a"), R("f(bool)", "a")] };
        var t = new SchemaSnapshot("public", []) { Routines = [R("f(int)", "b"), R("f(text)", "a"), R("f(date)", "a")] };
        var d = Cmp(s, t).Differences;
        Assert.Equal(
            [("f(bool)", DifferenceKind.Added), ("f(date)", DifferenceKind.Removed), ("f(int)", DifferenceKind.Modified)],
            d.OrderBy(x => x.Kind).Select(x => (x.Name, x.Kind)).OrderBy(x => x.Item1, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Compare_NamesDifferingByCase_Match()
    {
        var s = Snap(Tbl("Users", [Col("ID", pk: true)]));
        var t = Snap(Tbl("users", [Col("id", pk: true)]));
        Assert.Empty(Cmp(s, t).Differences);
    }

    [Fact]
    public void Compare_Result_OrderedByCategorySchemaName()
    {
        var s = Snap(Tbl("b"), Tbl("a"), View("z", "x"));
        var names = Cmp(s, Snap()).Differences.Select(d => d.Name);
        Assert.Equal(["a", "b", "z"], names);
        Assert.Equal(names, Cmp(Snap(View("z", "x"), Tbl("a"), Tbl("b")), Snap()).Differences.Select(d => d.Name));
    }

    [Fact]
    public void Compare_FiveThousandTables_UnderOneSecond()
    {
        var tables = Enumerable.Range(0, 5000).Select(i => Tbl($"t{i}", [Col("id", pk: true), Col("x")])).ToArray();
        var changed = Enumerable.Range(0, 5000).Select(i => Tbl($"t{i}", [Col("id", pk: true), Col("x", "text")])).ToArray();
        var sw = Stopwatch.StartNew();
        var c = Cmp(Snap(tables), Snap(changed));
        sw.Stop();
        Assert.Equal(5000, c.Count(DifferenceKind.Modified));
        Assert.True(sw.ElapsedMilliseconds < 1000, $"took {sw.ElapsedMilliseconds} ms");
    }

    // ---- two databases of one engine (MySQL): the schema is the database name ----

    private static TableInfo In(string db, string name, IReadOnlyList<ColumnDefinition>? cols = null, IReadOnlyList<ForeignKeyInfo>? fks = null) =>
        new(db, name, TableKind.Table, cols ?? [Col("id", pk: true)], [], fks ?? []);

    [Fact]
    public void Compare_DifferentDefaultSchemas_SameTableWithExtraColumn_IsOneModified()
    {
        var s = new SchemaSnapshot("a", [In("a", "t", [Col("id", pk: true), Col("x")])]);
        var t = new SchemaSnapshot("b", [In("b", "t")]);
        var d = Single(Cmp(s, t));
        Assert.Equal((DifferenceKind.Modified, (string?)null, "t"), (d.Kind, d.Schema, d.Name));
    }

    [Fact]
    public void Compare_DifferentDefaultSchemas_TableOnlyInSource_AddedWithNullSchema()
    {
        var d = Single(Cmp(new SchemaSnapshot("a", [In("a", "t")]), new SchemaSnapshot("b", [])));
        Assert.Equal((DifferenceKind.Added, (string?)null), (d.Kind, d.Schema));
    }

    [Fact]
    public void Compare_DifferentDefaultSchemas_ForeignKeyToOwnDatabase_Equal()
    {
        static IReadOnlyList<ForeignKeyInfo> Fk(string db) => [new("fk", ["pid"], db, "p", ["id"])];
        var s = new SchemaSnapshot("a", [In("a", "p"), In("a", "c", [Col("id", pk: true), Col("pid")], Fk("a"))]);
        var t = new SchemaSnapshot("b", [In("b", "p"), In("b", "c", [Col("id", pk: true), Col("pid")], Fk("b"))]);
        Assert.Empty(Cmp(s, t).Differences);
    }

    [Fact]
    public void Compare_DifferentDefaultSchemas_RoutineWithSameSignature_Equal()
    {
        static SchemaSnapshot Snapshot(string db) => new(db, [])
        {
            Routines = [new RoutineInfo(db, "f", RoutineKind.Function, "f(IN a int)", "int", "RETURN a")],
        };
        Assert.Empty(Cmp(Snapshot("a"), Snapshot("b")).Differences);
    }

    [Fact]
    public void Compare_DifferentDefaultSchemas_NonDefaultSchemaKeepsItsName()
    {
        var d = Single(Cmp(new SchemaSnapshot("a", [In("other", "t")]), new SchemaSnapshot("b", [])));
        Assert.Equal("other", d.Schema);
    }
}
