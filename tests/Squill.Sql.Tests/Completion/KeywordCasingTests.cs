using Squill.Core;
using Squill.Sql.Completion;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class KeywordCasingTests
{
    private static string[] Labels(string marked, KeywordCase keywordCase, SchemaSnapshot? schema = null)
    {
        var offset = marked.IndexOf('|', StringComparison.Ordinal);
        var request = new SqlRequest(marked.Remove(offset, 1), offset, DatabaseDialect.PostgreSql, schema, keywordCase);
        return [.. new KeywordCompletionProvider().GetCompletions(request).Select(i => i.Label)];
    }

    [Fact]
    public void UpperAndLowerIgnoreTheScript()
    {
        Assert.Contains("GROUP BY", Labels("select 1 from t |", KeywordCase.Upper));
        Assert.Contains("group by", Labels("SELECT 1 FROM t |", KeywordCase.Lower));
    }

    [Theory]
    [InlineData("select 1 from t |", "group by")]
    [InlineData("SELECT 1 FROM t |", "GROUP BY")]
    [InlineData("select 1 from t where x = 1 AND y = 2 |", "group by")]
    [InlineData("SELECT 1 from t WHERE x = 1 |", "GROUP BY")]
    [InlineData("sel|", "select")]
    [InlineData("SEL|", "SELECT")]
    [InlineData("|", "SELECT")]
    public void PreserveFollowsTheScript(string marked, string expected)
        => Assert.Contains(expected, Labels(marked, KeywordCase.Preserve));

    [Fact]
    public void FilterTextStaysUpperCanonical()
    {
        var items = new KeywordCompletionProvider().GetCompletions(new SqlRequest("sel", 3, DatabaseDialect.PostgreSql, null, KeywordCase.Lower));
        Assert.Equal("SELECT", items.First(i => i.Label == "select").FilterText);
    }

    [Fact]
    public void DefaultRequestIsUpper()
        => Assert.Contains("SELECT", new KeywordCompletionProvider().GetCompletions(new SqlRequest("sel", 3, DatabaseDialect.PostgreSql, null)).Select(i => i.Label));

    [Fact]
    public void GeneratedJoinTextFollowsTheCase()
    {
        var pk = new ColumnDefinition("id", "int", false, true, null);
        var schema = new SchemaSnapshot("public",
        [
            new TableInfo("public", "customers", TableKind.Table, [pk], [], []),
            new TableInfo("public", "orders", TableKind.Table, [pk, new ColumnDefinition("customer_id", "int", true, false, null)], [],
                [new ForeignKeyInfo("fk", ["customer_id"], null, "customers", ["id"])]),
        ]);
        var request = new SqlRequest("select * from customers c join ", 31, DatabaseDialect.PostgreSql, schema, KeywordCase.Lower);
        var row = new SchemaCompletionProvider().GetCompletions(request).First(i => i.Detail?.StartsWith("on ", StringComparison.Ordinal) == true);
        Assert.Equal("orders o on c.id = o.customer_id", row.InsertText);
    }
}
