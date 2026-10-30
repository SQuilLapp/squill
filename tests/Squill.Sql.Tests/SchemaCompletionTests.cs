using Squill.Core;
using Squill.Sql.Completion;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class SchemaCompletionTests
{
    private static TableInfo Table(string schema, string name, TableKind kind, params (string Name, string Type)[] columns) =>
        new(schema, name, kind, [.. columns.Select(c => new ColumnDefinition(c.Name, c.Type, true, false, null))], [], []);

    private static readonly SchemaSnapshot Schema = new("public",
    [
        Table("public", "users", TableKind.Table, ("id", "int"), ("name", "text"), ("Odd Name", "text")),
        Table("public", "orders", TableKind.Table, ("id", "int"), ("user_id", "int")),
        Table("public", "active_users", TableKind.View, ("id", "int")),
        Table("audit", "log", TableKind.Table, ("entry", "text")),
    ]);

    private static IReadOnlyList<CompletionItem> At(string marked, DatabaseDialect dialect = DatabaseDialect.PostgreSql, SchemaSnapshot? schema = null)
    {
        var offset = marked.IndexOf('|');
        return new SchemaCompletionProvider().GetCompletions(new SqlRequest(marked.Remove(offset, 1), offset, dialect, schema ?? Schema));
    }

    private static string[] Labels(string marked, DatabaseDialect dialect = DatabaseDialect.PostgreSql) =>
        [.. At(marked, dialect).Select(i => i.Label)];

    [Fact]
    public void NoSchema_YieldsNothing()
    {
        var offset = 14;
        Assert.Empty(new SchemaCompletionProvider().GetCompletions(new SqlRequest("select * from ", offset, DatabaseDialect.PostgreSql, null)));
    }

    [Theory]
    [InlineData("select * from |")]
    [InlineData("select * from users u join |")]
    [InlineData("select * from users, |")]
    [InlineData("insert into |")]
    [InlineData("update |")]
    public void TablePosition_OffersTablesViewsAndSchemas(string marked)
    {
        var items = At(marked);
        Assert.Equal(["active_users", "audit", "log", "orders", "public", "users"], items.Select(i => i.Label).Order(StringComparer.Ordinal).ToArray());
        Assert.Contains(items, i => i is { Label: "users", Kind: CompletionKind.Struct, Detail: "table" });
        Assert.Contains(items, i => i is { Label: "active_users", Kind: CompletionKind.Struct, Detail: "view" });
        Assert.Contains(items, i => i is { Label: "audit", Kind: CompletionKind.Module });
    }

    [Fact]
    public void SingleSchema_OffersNoSchemaItem()
    {
        var one = new SchemaSnapshot("public", [Table("public", "t", TableKind.Table, ("a", "int"))]);
        Assert.DoesNotContain(At("select * from |", schema: one), i => i.Kind == CompletionKind.Module);
    }

    [Fact]
    public void AfterSchemaDot_OffersOnlyThatSchemasTables()
    {
        Assert.Equal(["log"], Labels("select * from audit.|"));
        Assert.Equal(["log"], Labels("select * from audit.lo|"));
    }

    [Fact]
    public void SelectList_OffersColumnsOfEverySourceAndTheirNames()
    {
        var items = At("select | from users u join orders o on o.user_id = u.id");
        Assert.Contains(items, i => i is { Label: "name", Kind: CompletionKind.Field, Detail: "text", SortText: "0name" });
        Assert.Contains(items, i => i is { Label: "user_id", Detail: "int" });
        Assert.Contains(items, i => i is { Label: "u", Kind: CompletionKind.Class, SortText: "1u" });
        Assert.Contains(items, i => i.Label == "o");
    }

    [Fact]
    public void SharedColumnName_KeepsBareLabelAndShowsSourceInDetail()
    {
        var ids = At("select | from users u join orders o on 1 = 1").Where(i => i.Label == "id").ToList();
        Assert.Equal(2, ids.Count);
        Assert.Contains(ids, i => i.Detail == "u.int");
        Assert.Contains(ids, i => i.Detail == "o.int");
    }

    [Theory]
    [InlineData("select * from users where |")]
    [InlineData("select * from users u join orders o on |")]
    [InlineData("select name from users group by |")]
    [InlineData("select name from users order by |")]
    [InlineData("update users set |")]
    public void ColumnClauses_OfferColumns(string marked) => Assert.Contains("name", Labels(marked));

    [Fact]
    public void AliasDot_OffersOnlyThatSourcesColumns()
    {
        Assert.Equal(["id", "user_id"], Labels("select o.| from users u join orders o on 1 = 1").Order().ToArray());
        Assert.Equal(["id", "user_id"], Labels("select o.us| from users u join orders o on 1 = 1").Order().ToArray());
    }

    [Fact]
    public void TableDot_OffersThatTablesColumns()
    {
        Assert.Equal(["id", "user_id"], Labels("select orders.| from orders").Order().ToArray());
        Assert.Equal(["entry"], Labels("select audit.log.| from audit.log"));
    }

    [Fact]
    public void AliasHidesTableName() => Assert.Empty(Labels("select users.| from users u"));

    [Fact]
    public void CteColumns_AreOffered()
    {
        var items = At("with c as (select id as ident, name from users) select c.| from c");
        Assert.Equal(["ident", "name"], items.Select(i => i.Label).Order().ToArray());
        Assert.All(items, i => Assert.Null(i.Detail));
    }

    [Fact]
    public void DerivedTableWithStar_YieldsNothing() => Assert.Empty(Labels("select d.| from (select * from users) d"));

    [Fact]
    public void DerivedTableColumns_AreOffered() =>
        Assert.Equal(["n"], Labels("select d.| from (select name as n from users) d"));

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql, "\"Odd Name\"")]
    [InlineData(DatabaseDialect.Sqlite, "\"Odd Name\"")]
    [InlineData(DatabaseDialect.MySql, "`Odd Name`")]
    [InlineData(DatabaseDialect.SqlServer, "[Odd Name]")]
    public void NonPlainName_IsQuotedPerDialect(DatabaseDialect dialect, string expected)
    {
        var item = At("select u.| from users u", dialect).Single(i => i.Label == "Odd Name");
        Assert.Equal(expected, item.InsertText);
    }

    [Fact]
    public void PlainName_IsInsertedUnquoted() =>
        Assert.Equal("name", At("select | from users").First(i => i.Label == "name").InsertText);

    [Fact]
    public void ReplaceRange_CoversTypedPrefix()
    {
        var items = At("select u.na| from users u");
        Assert.All(items, i => Assert.Equal(new TextRange(9, 2), i.ReplaceRange));
    }

    [Fact]
    public void Composite_OrdersColumnsBeforeAliasesBeforeKeywords()
    {
        var request = new SqlRequest("select  from users u", 7, DatabaseDialect.PostgreSql, Schema);
        var items = new CompositeCompletionProvider(new KeywordCompletionProvider(), new SchemaCompletionProvider()).GetCompletions(request);
        var kinds = items.Select(i => i.Kind).ToList();
        Assert.True(kinds.LastIndexOf(CompletionKind.Field) < kinds.IndexOf(CompletionKind.Class));
        Assert.True(kinds.LastIndexOf(CompletionKind.Class) < kinds.IndexOf(CompletionKind.Keyword));
    }
}
