using Squill.Core;
using Squill.Sql.Analysis;
using Squill.Sql.Completion;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class TableRelationFinderTests
{
    private static ForeignKeyInfo Fk(string name, string[] cols, string table, string[] refCols, string? schema = null) =>
        new(name, cols, schema, table, refCols);

    private static TableInfo T(string name, string[] columns, string[] pk, string? schema = "public", params ForeignKeyInfo[] fks) =>
        new(schema, name, TableKind.Table, [.. columns.Select(c => new ColumnDefinition(c, "int", true, pk.Contains(c, StringComparer.OrdinalIgnoreCase), null))], [], fks);

    private static VisibleSource Src(string name, string? alias = null, string? schema = null) => new(schema, name, alias, null);

    private static string Describe(TableRelation r) =>
        $"{r.Origin}:{r.Existing.Name}({string.Join(",", r.ExistingColumns)})~{r.Other.Name}({string.Join(",", r.OtherColumns)}){(r.OtherReferencesExisting ? ">" : "<")}{r.ForeignKeyName}";

    private static readonly TableInfo Customers = T("customers", ["id", "name"], ["id"]);
    private static readonly TableInfo Orders = T("orders", ["id", "customer_id"], ["id"], "public", Fk("fk_o_c", ["customer_id"], "customers", ["id"]));

    private static SchemaSnapshot Snap(params TableInfo[] tables) => new("public", tables);

    private static string[] Run(SchemaSnapshot schema, TableInfo candidate, params VisibleSource[] left) =>
        [.. TableRelationFinder.Find(schema, left, candidate).Select(Describe)];

    [Fact]
    public void ChildToParent() =>
        Assert.Equal(["ForeignKey:customers(id)~orders(customer_id)>fk_o_c"], Run(Snap(Customers, Orders), Orders, Src("customers")));

    [Fact]
    public void ParentToChild() =>
        Assert.Equal(["ForeignKey:orders(customer_id)~customers(id)<fk_o_c"], Run(Snap(Customers, Orders), Customers, Src("orders")));

    [Fact]
    public void CompositeKeyKeepsOrder()
    {
        var parent = T("p", ["a", "b"], ["a", "b"]);
        var child = T("c", ["x", "y"], [], "public", Fk("fk", ["y", "x"], "p", ["b", "a"]));
        Assert.Equal(["ForeignKey:p(b,a)~c(y,x)>fk"], Run(Snap(parent, child), child, Src("p")));
    }

    [Fact]
    public void TwoKeysBetweenTheSamePair_AreOrderedByConstraintName()
    {
        var orders = T("orders", ["id", "bill", "ship"], ["id"], "public", Fk("z_ship", ["ship"], "customers", ["id"]), Fk("a_bill", ["bill"], "customers", ["id"]));
        Assert.Equal(
            ["ForeignKey:customers(id)~orders(bill)>a_bill", "ForeignKey:customers(id)~orders(ship)>z_ship"],
            Run(Snap(Customers, orders), orders, Src("customers")));
    }

    [Fact]
    public void SelfReference_GivesTwoRelations()
    {
        var employees = T("employees", ["id", "manager_id"], ["id"], "public", Fk("fk_m", ["manager_id"], "employees", ["id"]));
        Assert.Equal(
            ["ForeignKey:employees(id)~employees(manager_id)>fk_m", "ForeignKey:employees(manager_id)~employees(id)<fk_m"],
            Run(Snap(employees), employees, Src("employees", "e")));
    }

    [Fact]
    public void NearestSourceComesFirst()
    {
        var items = T("items", ["id", "order_id", "customer_id"], ["id"], "public", Fk("fk_i_o", ["order_id"], "orders", ["id"]), Fk("fk_i_c", ["customer_id"], "customers", ["id"]));
        Assert.Equal(
            ["ForeignKey:orders(id)~items(order_id)>fk_i_o", "ForeignKey:customers(id)~items(customer_id)>fk_i_c"],
            Run(Snap(Customers, Orders, items), items, Src("customers"), Src("orders")));
    }

    [Fact]
    public void ReferencedSchemaIsMatchedOnlyWhenWritten()
    {
        var a = T("t", ["id"], ["id"], "a");
        var b = T("t", ["id"], ["id"], "b");
        var child = T("c", ["t_id"], [], "public", Fk("fk", ["t_id"], "t", ["id"], "b"));
        var schema = Snap(a, b, child);
        Assert.Empty(Run(schema, child, Src("t", schema: "a")));
        Assert.Single(Run(schema, child, Src("t", schema: "b")));
    }

    [Fact]
    public void DerivedSourcesAreSkipped() =>
        Assert.Empty(Run(Snap(Customers, Orders), Orders, new VisibleSource(null, "customers", null, ["id"])));

    [Fact]
    public void MismatchedKeyLengthsAreIgnored()
    {
        var bad = T("bad", ["x"], [], "public", Fk("fk", ["x"], "customers", ["id", "name"]));
        Assert.Empty(Run(Snap(Customers, bad), bad, Src("customers")));
    }

    [Fact]
    public void SameNameFallback_UsesAPrimaryKeyColumnButNeverId()
    {
        var cust = T("customers", ["id", "customer_no"], ["customer_no"]);
        var ord = T("orders", ["id", "customer_no"], ["id"]);
        Assert.Equal(["SameColumnName:customers(customer_no)~orders(customer_no)>"], Run(Snap(cust, ord), ord, Src("customers")));
        var plain = T("a", ["id"], ["id"]);
        var other = T("b", ["id"], ["id"]);
        Assert.Empty(Run(Snap(plain, other), other, Src("a")));
    }

    [Fact]
    public void SameNameFallback_IsNotUsedWhenAKeyExists()
    {
        var cust = T("customers", ["id", "customer_no"], ["id"]);
        var ord = T("orders", ["id", "customer_no", "customer_id"], ["id"], "public", Fk("fk", ["customer_id"], "customers", ["id"]));
        Assert.Equal(["ForeignKey:customers(id)~orders(customer_id)>fk"], Run(Snap(cust, ord), ord, Src("customers")));
    }

    [Fact]
    public void SameNameFallback_CompositeKeyNeedsEveryColumn()
    {
        var lines = T("lines", ["order_id", "line_no"], ["order_id", "line_no"]);
        var partial = T("notes", ["order_id"], []);
        var full = T("shipments", ["order_id", "line_no"], []);
        Assert.Empty(Run(Snap(lines, partial), partial, Src("lines")));
        Assert.Equal(["SameColumnName:lines(order_id,line_no)~shipments(order_id,line_no)>"], Run(Snap(lines, full), full, Src("lines")));
    }

    [Fact]
    public void UnrelatedCandidate_GivesNothing() =>
        Assert.Empty(Run(Snap(Customers, T("misc", ["x"], [])), T("misc", ["x"], []), Src("customers")));

    [Fact]
    public void NamesAreCaseInsensitive() =>
        Assert.Single(Run(Snap(Customers, Orders), Orders, Src("CUSTOMERS")));
}
