using Squill.Core;
using Squill.Sql.Completion;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class QualifiedColumnTests
{
    private static ColumnDefinition Col(string name) => new(name, "int", true, false, null);

    private static TableInfo T(string name, params string[] columns) => new("public", name, TableKind.Table, [.. columns.Select(Col)], [], []);

    private static readonly SchemaSnapshot Schema = new("public",
    [
        T("users", "id", "name"),
        T("orders", "id", "user_id"),
    ]);

    private static CompletionItem[] Fields(string marked, DatabaseDialect dialect = DatabaseDialect.PostgreSql, SchemaSnapshot? schema = null)
    {
        var offset = marked.IndexOf('|', StringComparison.Ordinal);
        return [.. new SchemaCompletionProvider().GetCompletions(new SqlRequest(marked.Remove(offset, 1), offset, dialect, schema ?? Schema))
            .Where(i => i.Kind == CompletionKind.Field)];
    }

    private static string[] Inserts(string marked, DatabaseDialect dialect = DatabaseDialect.PostgreSql, SchemaSnapshot? schema = null)
        => [.. Fields(marked, dialect, schema).Select(i => i.InsertText)];

    [Fact]
    public void SharedColumnIsQualifiedWithTheAlias()
    {
        var items = Fields("select | from users u join orders o on u.id = o.user_id");
        Assert.Equal(["u.id", "name", "o.id", "user_id"], items.Select(i => i.InsertText));
        Assert.Equal(["id", "name", "id", "user_id"], items.Select(i => i.Label));
        Assert.Equal(["id", "name", "id", "user_id"], items.Select(i => i.FilterText));
        Assert.Equal(["u.int", "int", "o.int", "int"], items.Select(i => i.Detail));
    }

    [Fact]
    public void TableNameIsUsedWithoutAlias()
        => Assert.Equal(["users.id", "name", "orders.id", "user_id"], Inserts("select | from users join orders on 1 = 1"));

    [Fact]
    public void SingleTableStaysBare()
        => Assert.Equal(["id", "name"], Inserts("select | from users"));

    [Fact]
    public void TypedQualifierStaysBare()
        => Assert.Equal(["id", "name"], Inserts("select u.| from users u join orders o on 1 = 1"));

    [Theory]
    [InlineData(DatabaseDialect.MySql, "`my alias`.id")]
    [InlineData(DatabaseDialect.SqlServer, "[my alias].id")]
    [InlineData(DatabaseDialect.PostgreSql, "\"my alias\".id")]
    public void QualifierIsQuotedPerDialect(DatabaseDialect dialect, string expected)
        => Assert.Equal(expected, Inserts("select | from users \"my alias\" join orders o on 1 = 1".Replace("\"my alias\"", dialect switch { DatabaseDialect.MySql => "`my alias`", DatabaseDialect.SqlServer => "[my alias]", _ => "\"my alias\"" }), dialect)[0]);

    [Fact]
    public void CteColumnSharedWithATableIsQualified()
        => Assert.Equal(["c.id", "o.id", "user_id"], Inserts("with c as (select 1 as id) select | from c join orders o on 1 = 1").Where(s => s != "name"));

    [Fact]
    public void AnonymousDerivedTableStaysBare()
        => Assert.Equal(["id", "o.id", "user_id"], Inserts("select | from (select 1 as id) join orders o on 1 = 1").Distinct().Where(s => s != "name"));

    [Fact]
    public void OuterStatementColumnsAreNeverQualified()
        => Assert.Equal(["id", "user_id"], Inserts("select * from users u where exists (select | from orders o)").Take(2));
}
