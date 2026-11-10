using System.Diagnostics;
using Squill.Core;
using Squill.Sql.Completion;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class JoinRowsTests
{
    private static ColumnDefinition Col(string name, bool pk = false) => new(name, "int", true, pk, null);

    private static ForeignKeyInfo Fk(string name, string[] columns, string table, string[] referenced, ReferentialAction onDelete = ReferentialAction.NoAction)
        => new(name, columns, null, table, referenced, onDelete);

    private static TableInfo T(string name, ColumnDefinition[] columns, params ForeignKeyInfo[] fks)
        => new("public", name, TableKind.Table, columns, [], fks);

    private static readonly SchemaSnapshot Schema = new("public",
    [
        T("customers", [Col("id", true), Col("name")]),
        T("orders", [Col("id", true), Col("line"), Col("customer_id")],
            Fk("fk_orders_customer", ["customer_id"], "customers", ["id"], ReferentialAction.Cascade)),
        T("products", [Col("id", true)]),
        T("order_items", [Col("order_id"), Col("order_line"), Col("product_id")],
            Fk("fk_items_order", ["order_id", "order_line"], "orders", ["id", "line"]),
            Fk("fk_items_product", ["product_id"], "products", ["id"])),
        T("employees", [Col("id", true), Col("manager_id")], Fk("fk_manager", ["manager_id"], "employees", ["id"])),
        T("tags", [Col("id", true), Col("name")]),
    ]);

    private static IReadOnlyList<CompletionItem> At(string marked, DatabaseDialect dialect = DatabaseDialect.PostgreSql, SchemaSnapshot? schema = null)
    {
        var offset = marked.IndexOf('|', StringComparison.Ordinal);
        return new SchemaCompletionProvider().GetCompletions(new SqlRequest(marked.Remove(offset, 1), offset, dialect, schema ?? Schema));
    }

    private static CompletionItem[] Rows(string marked, DatabaseDialect dialect = DatabaseDialect.PostgreSql, SchemaSnapshot? schema = null)
        => [.. At(marked, dialect, schema).Where(i => i.SortText!.StartsWith("00", StringComparison.Ordinal)).OrderBy(i => i.SortText, StringComparer.Ordinal)];

    [Fact]
    public void FirstRowIsTheForeignKeyTableWithAliasAndCondition()
    {
        var row = Rows("select * from customers c join |")[0];
        Assert.Equal("orders o", row.Label);
        Assert.Equal("orders o ON c.id = o.customer_id", row.InsertText);
        Assert.Equal("ON c.id = o.customer_id", row.Detail);
        Assert.Equal("orders", row.FilterText);
        Assert.Equal(CompletionKind.Struct, row.Kind);
        Assert.Equal("orders.customer_id -> customers.id (fk_orders_customer) ON DELETE CASCADE, ON UPDATE NO ACTION", row.Documentation);
    }

    [Fact]
    public void NoAliasWhenNoSourceHasOne()
        => Assert.Equal("orders ON customers.id = orders.customer_id", Rows("select * from customers join |")[0].InsertText);

    [Fact]
    public void NearestSourceFirstThenRepeatsAfterNewTables()
    {
        var rows = Rows("select * from customers c join orders o on c.id = o.customer_id join |");
        Assert.Equal(["order_items oi", "customers cu", "orders ord"], rows.Select(r => r.Label).ToArray());
        Assert.Equal("ON o.id = oi.order_id AND o.line = oi.order_line", rows[0].Detail);
        Assert.Equal("ON o.customer_id = cu.id", rows[1].Detail);
        Assert.Equal("ON c.id = ord.customer_id", rows[2].Detail);
    }

    [Fact]
    public void TwoKeysBetweenTheSamePairGiveTwoRows()
    {
        var schema = new SchemaSnapshot("public",
        [
            T("customers", [Col("id", true)]),
            T("shipments", [Col("id", true), Col("from_c"), Col("to_c")],
                Fk("fk_from", ["from_c"], "customers", ["id"]), Fk("fk_to", ["to_c"], "customers", ["id"])),
        ]);
        Assert.Equal(["ON customers.id = shipments.from_c", "ON customers.id = shipments.to_c"],
            Rows("select * from customers join |", schema: schema).Select(r => r.Detail).ToArray());
    }

    [Fact]
    public void SelfJoinGivesTwoAliasedRows()
    {
        var rows = Rows("select * from employees e join |");
        Assert.Equal(["employees em", "employees em"], rows.Select(r => r.Label).ToArray());
        Assert.Equal(["ON e.id = em.manager_id", "ON e.manager_id = em.id"], rows.Select(r => r.Detail).ToArray());
    }

    [Theory]
    [InlineData("left join")]
    [InlineData("right join")]
    [InlineData("full outer join")]
    [InlineData("inner join")]
    public void EveryConditionJoinKindUsesTheSameSideOrder(string join)
        => Assert.Equal("orders o ON c.id = o.customer_id", Rows($"select * from customers c {join} |")[0].InsertText);

    [Theory]
    [InlineData("select * from customers c cross join |")]
    [InlineData("select * from customers c natural join |")]
    [InlineData("select * from customers c, |")]
    [InlineData("select * from customers c join | on 1 = 1")]
    public void NoRowsWhereNoConditionIsWanted(string marked) => Assert.Empty(Rows(marked));

    [Fact]
    public void CrossApplyGeneratesNothing()
        => Assert.Empty(Rows("select * from customers c cross apply |", DatabaseDialect.SqlServer));

    [Fact]
    public void UnrelatedTableOnlyHasItsPlainRow()
    {
        var items = At("select * from customers c join |");
        Assert.DoesNotContain(Rows("select * from customers c join |"), r => r.FilterText == "tags");
        Assert.Contains(items, i => i is { Label: "tags", SortText: "05tags" });
    }

    [Fact]
    public void PlainRowsStayAfterTheJoinRows()
    {
        var items = At("select * from customers c join |").OrderBy(i => i.SortText, StringComparer.Ordinal).ToList();
        Assert.Equal("orders o", items[0].Label);
        Assert.Equal("customers", items[1].Label);
    }

    [Fact]
    public void NothingForDerivedSourcesOrWithoutSchema()
    {
        Assert.Empty(Rows("select * from (select 1 as x) d join |"));
        var offset = 14;
        Assert.Empty(new SchemaCompletionProvider().GetCompletions(new SqlRequest("select * from ", offset, DatabaseDialect.PostgreSql, null)));
    }

    [Theory]
    [InlineData(DatabaseDialect.MySql, "child ON Parent.id = child.`Parent Id`")]
    [InlineData(DatabaseDialect.SqlServer, "child ON Parent.id = child.[Parent Id]")]
    [InlineData(DatabaseDialect.PostgreSql, "child ON \"Parent\".id = child.\"Parent Id\"")]
    public void QuotesPerDialect(DatabaseDialect dialect, string expected)
    {
        var schema = new SchemaSnapshot("public",
        [
            T("Parent", [Col("id", true)]),
            T("child", [Col("Parent Id")], Fk("fk", ["Parent Id"], "Parent", ["id"])),
        ]);
        Assert.Equal(expected, Rows("select * from Parent join |", dialect, schema)[0].InsertText);
    }

    [Fact]
    public void SchemaQualifiedCandidateWhenBareNameIsAnotherTable()
    {
        var schema = new SchemaSnapshot("public",
        [
            T("customers", [Col("id", true)]),
            T("items", [Col("id", true)]),
            new TableInfo("audit", "items", TableKind.Table, [Col("customer_id")], [], [Fk("fk", ["customer_id"], "customers", ["id"])]),
        ]);
        var row = Rows("select * from customers join |", schema: schema)[0];
        Assert.Equal("audit.items", row.Label);
        Assert.Equal("audit.items ON customers.id = items.customer_id", row.InsertText);
        Assert.Equal("items", row.FilterText);
    }

    [Fact]
    public void AcceptingReplacesTheTypedPrefix()
    {
        var marked = "select * from customers c join ord|";
        var offset = marked.IndexOf('|', StringComparison.Ordinal);
        var text = marked.Remove(offset, 1);
        var item = new SchemaCompletionProvider().GetCompletions(new SqlRequest(text, offset, DatabaseDialect.PostgreSql, Schema))
            .First(i => i.Label == "orders o");
        var range = item.ReplaceRange;
        Assert.Equal("select * from customers c join orders o ON c.id = o.customer_id",
            text.Remove(range.Start, range.Length).Insert(range.Start, item.InsertText));
    }

    [Fact]
    public void LargeSchemaStaysInteractive()
    {
        var tables = new List<TableInfo>();
        for (var i = 0; i < 5000; i++)
        {
            var fks = new List<ForeignKeyInfo>();
            if (i >= 2)
            {
                fks.Add(Fk($"fk_a{i}", ["a"], $"t{i / 2}", ["id"]));
                fks.Add(Fk($"fk_b{i}", ["b"], $"t{i / 3}", ["id"]));
            }

            tables.Add(new TableInfo(null, $"t{i}", TableKind.Table, [Col("id", true), Col("a"), Col("b")], [], fks));
        }

        var schema = new SchemaSnapshot(null, tables);
        var watch = Stopwatch.StartNew();
        var rows = Rows("select * from t1 x join |", schema: schema);
        watch.Stop();
        Assert.NotEmpty(rows);
        Assert.True(watch.ElapsedMilliseconds < 3000, $"took {watch.ElapsedMilliseconds} ms");
    }
}
