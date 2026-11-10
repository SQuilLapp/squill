using Squill.Core;
using Squill.Sql.Completion;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class SnippetCompletionTests
{
    private static IReadOnlyList<CompletionItem> Items(string marked, DatabaseDialect dialect = DatabaseDialect.PostgreSql, KeywordCase keywordCase = KeywordCase.Upper)
    {
        var offset = marked.IndexOf('|', StringComparison.Ordinal);
        return new SnippetCompletionProvider().GetCompletions(new SqlRequest(marked.Remove(offset, 1), offset, dialect, null, keywordCase));
    }

    private static string Insert(IReadOnlyList<CompletionItem> items, string label) => items.Single(i => i.Label == label).InsertText;

    [Theory]
    [InlineData("|")]
    [InlineData("select 1; |")]
    [InlineData("sel|")]
    public void SixTemplatesAtTheStartOfAStatement(string marked)
    {
        var items = Items(marked);
        Assert.Equal(["crt", "cte", "del", "ins", "sel", "upd"], items.Select(i => i.Label).Order());
        Assert.All(items, i =>
        {
            Assert.Equal(CompletionKind.Snippet, i.Kind);
            Assert.True(i.IsSnippet);
            Assert.Equal("09" + i.Label, i.SortText);
            Assert.Equal(i.Label, i.FilterText);
            Assert.NotNull(i.Detail);
            Assert.NotNull(i.Documentation);
        });
    }

    [Theory]
    [InlineData("select 1 from |")]
    [InlineData("select 1 where |")]
    [InlineData("select |")]
    public void NoneInsideAStatement(string marked) => Assert.Empty(Items(marked));

    [Fact]
    public void ExactTemplates()
    {
        var items = Items("|");
        Assert.Equal("SELECT ${1:*}\nFROM ${2:table}\nWHERE ${3:condition}$0", Insert(items, "sel"));
        Assert.Equal("INSERT INTO ${1:table} (${2:columns})\nVALUES (${3:values})$0", Insert(items, "ins"));
        Assert.Equal("UPDATE ${1:table}\nSET ${2:column} = ${3:value}\nWHERE ${4:condition}$0", Insert(items, "upd"));
        Assert.Equal("DELETE FROM ${1:table}\nWHERE ${2:condition}$0", Insert(items, "del"));
        Assert.Equal("WITH ${1:name} AS (\n    ${2:SELECT 1}\n)\nSELECT * FROM ${1:name}$0", Insert(items, "cte"));
        Assert.Equal("CREATE TABLE ${1:name} (\n    ${2:id} ${3:INT} PRIMARY KEY$0\n)", Insert(items, "crt"));
    }

    [Fact]
    public void LowerKeywordCase()
    {
        var items = Items("|", keywordCase: KeywordCase.Lower);
        Assert.Equal("select ${1:*}\nfrom ${2:table}\nwhere ${3:condition}$0", Insert(items, "sel"));
        Assert.Equal("create table ${1:name} (\n    ${2:id} ${3:INT} primary key$0\n)", Insert(items, "crt"));
    }

    [Fact]
    public void ToPlainFillsTheDefaults()
    {
        var items = Items("|");
        Assert.Equal("SELECT *\nFROM table\nWHERE condition", SnippetText.ToPlain(Insert(items, "sel")));
        Assert.Equal("WITH name AS (\n    SELECT 1\n)\nSELECT * FROM name", SnippetText.ToPlain(Insert(items, "cte")));
        Assert.Equal("DELETE FROM table\nWHERE condition", SnippetText.ToPlain(Insert(items, "del")));
    }

    [Fact]
    public void DocumentationIsTheExpandedText()
        => Assert.Contains("DELETE FROM table\nWHERE condition", Items("|").Single(i => i.Label == "del").Documentation);

    [Theory]
    [InlineData(DatabaseDialect.Sqlite, "INTEGER")]
    [InlineData(DatabaseDialect.MySql, "INT")]
    [InlineData(DatabaseDialect.SqlServer, "INT")]
    public void CreateTableTypeFollowsTheDialect(DatabaseDialect dialect, string type)
        => Assert.Contains("${3:" + type + "}", Insert(Items("|", dialect), "crt"));

    [Fact]
    public void TemplatesAreBalanced()
    {
        foreach (var i in Items("|"))
        {
            var t = i.InsertText;
            Assert.EndsWith("$0" + (i.Label == "crt" ? "\n)" : ""), t);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(t, @"\$0"));
            // every placeholder opens with ${n: and closes with one brace; no other brace or dollar remains
            var rest = System.Text.RegularExpressions.Regex.Replace(t, @"\$\{\d:[^}]*\}", "").Replace("$0", "", StringComparison.Ordinal);
            Assert.DoesNotContain('$', rest);
            Assert.DoesNotContain('}', rest);
        }
    }
}
