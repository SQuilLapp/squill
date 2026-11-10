using Squill.Core;
using Squill.Sql.Completion;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class OnClauseTests
{
    private static ColumnDefinition Col(string name, bool pk = false) => new(name, "int", true, pk, null);

    private static ForeignKeyInfo Fk(string name, string[] columns, string table, string[] referenced) => new(name, columns, null, table, referenced);

    private static TableInfo T(string name, ColumnDefinition[] columns, params ForeignKeyInfo[] fks) => new("public", name, TableKind.Table, columns, [], fks);

    private static readonly SchemaSnapshot Schema = new("public",
    [
        T("customers", [Col("id", true)]),
        T("orders", [Col("id", true), Col("line"), Col("customer_id")], Fk("fk_orders_customer", ["customer_id"], "customers", ["id"])),
        T("products", [Col("id", true)]),
        T("order_items", [Col("order_id"), Col("order_line"), Col("product_id")],
            Fk("fk_items_order", ["order_id", "order_line"], "orders", ["id", "line"]),
            Fk("fk_items_product", ["product_id"], "products", ["id"])),
    ]);

    private static CompletionItem[] At(string marked, DatabaseDialect dialect = DatabaseDialect.PostgreSql, SchemaSnapshot? schema = null)
    {
        var offset = marked.IndexOf('|', StringComparison.Ordinal);
        return [.. new SchemaCompletionProvider().GetCompletions(new SqlRequest(marked.Remove(offset, 1), offset, dialect, schema ?? Schema))
            .Where(i => i.SortText!.StartsWith("00", StringComparison.Ordinal)).OrderBy(i => i.SortText, StringComparer.Ordinal)];
    }

    private static string[] Labels(string marked, DatabaseDialect dialect = DatabaseDialect.PostgreSql, SchemaSnapshot? schema = null)
        => [.. At(marked, dialect, schema).Select(i => i.Label)];

    [Fact]
    public void ExistingSideIsWrittenFirst()
    {
        var item = At("select * from customers c join orders o on |")[0];
        Assert.Equal("c.id = o.customer_id", item.Label);
        Assert.Equal("c.id = o.customer_id", item.InsertText);
        Assert.Equal("fk_orders_customer", item.Detail);
        Assert.Equal(CompletionKind.Snippet, item.Kind);
        Assert.Equal("orders.customer_id -> customers.id (fk_orders_customer) ON DELETE NO ACTION, ON UPDATE NO ACTION", item.Documentation);
        Assert.Equal(["o.customer_id = c.id"], Labels("select * from orders o join customers c on |"));
    }

    [Theory]
    [InlineData("left join")]
    [InlineData("right join")]
    [InlineData("full outer join")]
    [InlineData("inner join")]
    [InlineData("join")]
    public void EveryJoinKindGivesTheSameRows(string join)
        => Assert.Equal(["c.id = o.customer_id"], Labels($"select * from customers c {join} orders o on |"));

    [Fact]
    public void CompositeKeyIsOneRow()
        => Assert.Equal(["o.id = oi.order_id AND o.line = oi.order_line"], Labels("select * from orders o join order_items oi on |"));

    private static readonly SchemaSnapshot TwoKeys = new("public",
    [
        T("customers", [Col("id", true)]),
        T("shipments", [Col("id", true), Col("from_c"), Col("to_c")],
            Fk("fk_from", ["from_c"], "customers", ["id"]), Fk("fk_to", ["to_c"], "customers", ["id"])),
    ]);

    [Fact]
    public void TwoKeysBetweenThePairGiveTwoRows()
        => Assert.Equal(["customers.id = shipments.from_c", "customers.id = shipments.to_c"],
            Labels("select * from customers join shipments on |", schema: TwoKeys));

    [Fact]
    public void SecondJoinOffersRelationsToEveryEarlierSourceNearestFirst()
    {
        var schema = new SchemaSnapshot("public",
        [
            T("a", [Col("id", true)]),
            T("b", [Col("id", true), Col("a_id")], Fk("fk_b_a", ["a_id"], "a", ["id"])),
            T("c", [Col("a_id"), Col("b_id")], Fk("fk_c_a", ["a_id"], "a", ["id"]), Fk("fk_c_b", ["b_id"], "b", ["id"])),
        ]);
        Assert.Equal(["y.id = z.b_id", "x.id = z.a_id"],
            Labels("select * from a x join b y on x.id = y.a_id join c z on |", schema: schema));
    }

    [Fact]
    public void NeverRepeatsARelationBetweenEarlierSources()
        => Assert.Equal(["o.id = oi.order_id AND o.line = oi.order_line"],
            Labels("select * from customers c join orders o on c.id = o.customer_id join order_items oi on |"));

    [Theory]
    [InlineData("select * from customers c join orders o on c.id = o.customer_id and |")]
    [InlineData("select * from customers c join orders o on o.customer_id = c.id and |")]
    [InlineData("select * from customers c join orders o on C.ID = O.Customer_Id and |")]
    public void WrittenConditionIsNotProposedAgain(string marked) => Assert.Empty(Labels(marked));

    [Fact]
    public void OnlyTheRemainingRelationIsProposed()
        => Assert.Equal(["customers.id = shipments.to_c"],
            Labels("select * from customers join shipments on customers.id = shipments.from_c and |", schema: TwoKeys));

    [Theory]
    [InlineData("select * from customers c cross join orders o on |")]
    [InlineData("select * from customers c natural join orders o on |")]
    [InlineData("select * from customers c join orders o using (|")]
    [InlineData("select * from (select 1 as x) d join orders o on |")]
    public void NothingWhereNoConditionApplies(string marked) => Assert.Empty(Labels(marked));

    [Theory]
    [InlineData(DatabaseDialect.MySql, "p.id = Child.`Parent Id`")]
    [InlineData(DatabaseDialect.SqlServer, "p.id = Child.[Parent Id]")]
    [InlineData(DatabaseDialect.PostgreSql, "p.id = \"Child\".\"Parent Id\"")]
    public void QuotesPerDialect(DatabaseDialect dialect, string expected)
    {
        var schema = new SchemaSnapshot("public",
        [
            T("parent", [Col("id", true)]),
            T("Child", [Col("Parent Id")], Fk("fk", ["Parent Id"], "parent", ["id"])),
        ]);
        Assert.Equal([expected], Labels("select * from parent p join Child on |", dialect, schema));
    }
}
