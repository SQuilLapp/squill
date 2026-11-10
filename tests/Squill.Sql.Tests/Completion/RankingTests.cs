using Squill.Core;
using Squill.Sql.Completion;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class RankingTests
{
    private static ColumnDefinition Col(string name, bool pk = false) => new(name, "int", true, pk, null);

    private static ForeignKeyInfo Fk(string name, string[] columns, string table, string[] referenced) => new(name, columns, null, table, referenced);

    private static TableInfo T(string name, ColumnDefinition[] columns, params ForeignKeyInfo[] fks) => new("public", name, TableKind.Table, columns, [], fks);

    private static readonly SchemaSnapshot Schema = new("public",
    [
        T("audit", [Col("id", true)]),
        T("customers", [Col("id", true), Col("name")]),
        T("orders", [Col("id", true), Col("customer_id"), Col("note"), Col("a_total")], Fk("fk_orders_customer", ["customer_id"], "customers", ["id"])),
        T("regions", [Col("id", true)]),
        T("zones", [Col("id", true)]),
    ]);

    private static CompletionItem[] At(string marked, SchemaSnapshot? schema = null)
    {
        var offset = marked.IndexOf('|', StringComparison.Ordinal);
        return [.. new SchemaCompletionProvider().GetCompletions(new SqlRequest(marked.Remove(offset, 1), offset, DatabaseDialect.PostgreSql, schema ?? Schema))
            .OrderBy(i => i.SortText, StringComparer.Ordinal)];
    }

    private static string[] Fields(string marked) => [.. At(marked).Where(i => i.Kind == CompletionKind.Field).Select(i => i.Label)];

    private static string[] Structs(string marked) => [.. At(marked).Where(i => i.Kind == CompletionKind.Struct).Select(i => i.Label)];

    [Fact]
    public void KeyColumnsComeFirst()
        => Assert.Equal(["customer_id", "id", "a_total", "note"], Fields("select * from orders o where |"));

    [Fact]
    public void RelatedTablesComeBeforeUnrelatedOnes()
        => Assert.Equal(["customers", "orders", "audit", "regions", "zones"], Structs("select * from customers c, |"));

    [Fact]
    public void TableUsedTwiceBeatsTableUsedOnce()
        => Assert.Equal(["zones", "regions", "audit"], Structs("select zones.id, zones.id, zones.id, regions.id from regions, |").Take(3));

    [Fact]
    public void InsertIntoHasNoRelatednessOrdering()
        => Assert.Equal(["audit", "customers", "orders", "regions", "zones"], Structs("insert into |"));

    [Fact]
    public void OnColumnsOfTheKeyPairComeFirst()
    {
        var labels = At("select * from customers c join orders o on |").Where(i => i.Kind == CompletionKind.Field).Select(i => i.Label + ":" + i.SortText![..2]).ToArray();
        Assert.Equal(["customer_id:01", "id:01", "a_total:02", "id:02", "name:02", "note:02"], labels.Where(l => !l.EndsWith(":03", StringComparison.Ordinal)));
    }

    [Fact]
    public void GroupConstantsAreOrdered()
    {
        string[] groups = [CompletionOrder.Relation, CompletionOrder.KeyColumn, CompletionOrder.Column, CompletionOrder.Qualifier,
            CompletionOrder.UsedTable, CompletionOrder.Table, CompletionOrder.Schema, CompletionOrder.Function, CompletionOrder.Keyword];
        Assert.Equal(["00", "01", "02", "03", "04", "05", "06", "07", "08"], groups);
    }

    [Fact]
    public void OrderIsStable()
        => Assert.Equal(At("select * from customers c, |").Select(i => i.Label + i.SortText), At("select * from customers c, |").Select(i => i.Label + i.SortText));
}
