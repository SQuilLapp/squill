using System.Diagnostics;
using Squill.Core;
using Squill.Sql.Analysis;
using Squill.Sql.Completion;
using Squill.Sql.Functions;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class CompletionDocumentationTests
{
    private static ColumnDefinition Col(string name, string type = "int", bool nullable = true, bool pk = false, string? def = null) => new(name, type, nullable, pk, def);

    private static ForeignKeyInfo Fk(string name, string[] cols, string table, string[] refs) => new(name, cols, null, table, refs);

    private static readonly TableInfo Customers = new("public", "customers", TableKind.Table, [Col("id", nullable: false, pk: true)], [], []);

    private static readonly TableInfo Orders = new("public", "orders", TableKind.Table,
        [Col("id", nullable: false, pk: true), Col("customer_id", def: "0"), Col("note", "text")], [],
        [Fk("fk_orders_customer", ["customer_id"], "customers", ["id"])]);

    private static readonly TableInfo Invoices = new("public", "invoices", TableKind.Table,
        [Col("customer_id")], [], [Fk("fk_inv", ["customer_id"], "customers", ["id"])]);

    private static readonly SchemaSnapshot Schema = new("public", [Customers, Orders, Invoices]);

    [Fact]
    public void TableWithForeignKey()
        => Assert.Equal(
            "**orders** - table, 3 columns\n\n```\nid int PK NOT NULL\ncustomer_id int\nnote text\n```\n\nForeign keys:\n- `fk_orders_customer: customer_id -> customers(id)`",
            CompletionDocumentation.Table(Orders));

    [Fact]
    public void WideTableIsCutAtTwelveColumns()
    {
        var wide = new TableInfo("public", "wide", TableKind.Table, [.. Enumerable.Range(0, 20).Select(i => Col("c" + i))], [], []);
        var text = CompletionDocumentation.Table(wide);
        Assert.Contains("c11 int\n...and 8 more\n```", text, StringComparison.Ordinal);
        Assert.DoesNotContain("c12", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewShowsItsDefinition()
    {
        var view = new TableInfo("public", "v", TableKind.View, [Col("a")], [], [], null, "select a\nfrom t");
        Assert.Equal("**v** - view, 1 columns\n\n```\na int\n```\n\n````\nselect a\nfrom t\n````", CompletionDocumentation.Table(view));
    }

    [Fact]
    public void ColumnWithDefaultAndForeignKey()
        => Assert.Equal("**o.customer_id**\n\n`int`, nullable, default `0`\n\nreferences `customers(id)`",
            CompletionDocumentation.Column(Schema, Orders, Orders.Columns[1], "o"));

    [Fact]
    public void ColumnReferencedByTwoTables()
        => Assert.Equal("**customers.id**\n\n`int`, not null, primary key\n\nreferenced by `orders(customer_id)`, `invoices(customer_id)`",
            CompletionDocumentation.Column(Schema, Customers, Customers.Columns[0], "customers"));

    [Fact]
    public void AliasAndDerivedSources()
    {
        Assert.Equal("alias of `public.orders`", CompletionDocumentation.Source(new VisibleSource("public", "orders", "o", null)));
        Assert.Equal("table `orders`", CompletionDocumentation.Source(new VisibleSource(null, "orders", null, null)));
        Assert.Equal("derived table, columns: a, b", CompletionDocumentation.Source(new VisibleSource(null, "c", null, ["a", "b"])));
    }

    [Fact]
    public void Functions()
    {
        Assert.Equal("`COUNT(expr) -> bigint`\n\nCounts.", CompletionDocumentation.Function(new FunctionSignature("COUNT", ["expr"], "bigint", "Counts.")));
        Assert.Equal("`f(int) -> int`", CompletionDocumentation.Function(new RoutineInfo("public", "f", RoutineKind.Function, "f(int)", "int", null)));
    }

    [Fact]
    public void JoinRowMentionsTheReferentialActions()
    {
        var orders = Orders with { ForeignKeys = [new ForeignKeyInfo("fk_orders_customer", ["customer_id"], null, "customers", ["id"], ReferentialAction.Cascade, ReferentialAction.SetNull)] };
        var schema = new SchemaSnapshot("public", [Customers, orders]);
        var row = new SchemaCompletionProvider().GetCompletions(new SqlRequest("select * from customers c join ", 31, DatabaseDialect.PostgreSql, schema))
            .First(i => i.Detail?.StartsWith("ON ", StringComparison.Ordinal) == true);
        Assert.Equal("orders.customer_id -> customers.id (fk_orders_customer) ON DELETE CASCADE, ON UPDATE SET NULL", row.Documentation);
    }

    [Fact]
    public void TextIsCappedAndBacktickNamesAreFenced()
    {
        var definition = string.Join('\n', Enumerable.Range(0, 12).Select(_ => new string('x', 300)));
        var view = new TableInfo("public", "v", TableKind.View, [Col("a")], [], [], null, definition);
        var text = CompletionDocumentation.Table(view);
        Assert.Equal(2000, text.Length);
        Assert.EndsWith("...", text, StringComparison.Ordinal);
        Assert.Equal("`` a`b ``", CompletionDocumentation.Code("a`b"));
    }

    [Fact]
    public void ProviderFillsTheDocumentation()
    {
        var items = new SchemaCompletionProvider().GetCompletions(new SqlRequest("select  from orders o", 7, DatabaseDialect.PostgreSql, Schema));
        Assert.All(items, i => Assert.False(string.IsNullOrEmpty(i.Documentation)));
        var tables = new SchemaCompletionProvider().GetCompletions(new SqlRequest("select * from ", 14, DatabaseDialect.PostgreSql, Schema));
        Assert.Equal(CompletionDocumentation.Table(Orders), tables.First(i => i.Label == "orders").Documentation);
    }

    [Fact]
    public void TablePositionOnALargeSchemaStaysInteractive()
    {
        var tables = Enumerable.Range(0, 5000)
            .Select(i => new TableInfo(null, $"t{i}", TableKind.Table, [Col("id", pk: true), Col("a"), Col("b")], [],
                i >= 2 ? [Fk($"fk_a{i}", ["a"], $"t{i / 2}", ["id"]), Fk($"fk_b{i}", ["b"], $"t{i / 3}", ["id"])] : []))
            .ToList();
        var request = new SqlRequest("select * from t1 x, ", 20, DatabaseDialect.PostgreSql, new SchemaSnapshot(null, tables));
        var watch = Stopwatch.StartNew();
        var items = new SchemaCompletionProvider().GetCompletions(request);
        watch.Stop();
        Assert.NotEmpty(items);
        Assert.True(watch.ElapsedMilliseconds < 3000, $"took {watch.ElapsedMilliseconds} ms");
    }
}
