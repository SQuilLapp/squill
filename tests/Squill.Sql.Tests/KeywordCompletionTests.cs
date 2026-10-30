using Squill.Core;
using Squill.Sql.Completion;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class KeywordCompletionTests
{
    private static IReadOnlyList<CompletionItem> At(string marked, DatabaseDialect dialect = DatabaseDialect.PostgreSql)
    {
        var offset = marked.IndexOf('|');
        return new KeywordCompletionProvider().GetCompletions(new SqlRequest(marked.Remove(offset, 1), offset, dialect, null));
    }

    private static string[] Labels(string marked, DatabaseDialect dialect = DatabaseDialect.PostgreSql) =>
        [.. At(marked, dialect).Select(i => i.Label)];

    [Theory]
    [InlineData("sel|", 0, 3)]
    [InlineData("select a from t where x_1|", 22, 3)]
    [InlineData("select | from t", 7, 0)]
    public void ReplaceRange_CoversTypedPrefix(string marked, int start, int length)
    {
        var items = At(marked);
        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Equal(new TextRange(start, length), i.ReplaceRange));
    }

    [Fact]
    public void Items_AreKeywordsInsertingTheirLabel() =>
        Assert.All(At("|"), i =>
        {
            Assert.Equal(CompletionKind.Keyword, i.Kind);
            Assert.Equal(i.Label, i.InsertText);
        });

    [Theory]
    [InlineData("|")]
    [InlineData("select 1; |")]
    public void StatementStart_OffersStatementKeywords(string marked)
    {
        var labels = Labels(marked);
        Assert.Contains("SELECT", labels);
        Assert.Contains("INSERT", labels);
        Assert.Contains("WITH", labels);
        Assert.DoesNotContain("WHERE", labels);
    }

    [Fact]
    public void AfterFromSource_OffersClauseContinuations()
    {
        var labels = Labels("select * from t |");
        Assert.Contains("JOIN", labels);
        Assert.Contains("WHERE", labels);
        Assert.Contains("GROUP BY", labels);
        Assert.Contains("ORDER BY", labels);
        Assert.Contains("LIMIT", labels);
    }

    [Fact]
    public void AfterFromKeyword_OffersNoKeyword() => Assert.Empty(Labels("select * from |", DatabaseDialect.Sqlite));

    [Fact]
    public void SelectList_OffersDistinctAndCase()
    {
        var labels = Labels("select |");
        Assert.Contains("DISTINCT", labels);
        Assert.Contains("CASE", labels);
    }

    [Fact]
    public void Where_OffersExpressionKeywords() => Assert.Contains("AND", Labels("select * from t where a = 1 |"));

    [Fact]
    public void Top_OnlyForTSql()
    {
        Assert.Contains("TOP", Labels("select |", DatabaseDialect.SqlServer));
        Assert.DoesNotContain("TOP", Labels("select |", DatabaseDialect.PostgreSql));
        Assert.DoesNotContain("TOP", Labels("select |", DatabaseDialect.MySql));
    }

    [Fact]
    public void Returning_OnlyForPostgres()
    {
        Assert.Contains("RETURNING", Labels("update t set a = 1 |"));
        Assert.DoesNotContain("RETURNING", Labels("update t set a = 1 |", DatabaseDialect.MySql));
        Assert.DoesNotContain("RETURNING", Labels("update t set a = 1 |", DatabaseDialect.SqlServer));
    }

    [Fact]
    public void StatementStart_HasDialectSpecificItems()
    {
        Assert.Contains("PRAGMA", Labels("|", DatabaseDialect.Sqlite));
        Assert.DoesNotContain("PRAGMA", Labels("|", DatabaseDialect.PostgreSql));
        Assert.Contains("MERGE", Labels("|", DatabaseDialect.SqlServer));
    }

    [Fact]
    public void Filter_PutsPrefixMatchesBeforeContains()
    {
        var r = new TextRange(0, 0);
        CompletionItem[] items =
        [
            new("GROUP BY", CompletionKind.Keyword, "GROUP BY", null, null, r),
            new("ORDER BY", CompletionKind.Keyword, "ORDER BY", null, null, r),
            new("WHERE", CompletionKind.Keyword, "WHERE", null, null, r),
        ];
        Assert.Equal(["ORDER BY", "GROUP BY"], CompletionFilter.Filter(items, "o").Select(i => i.Label));
        Assert.Empty(CompletionFilter.Filter(items, "zzz"));
        Assert.Same(items, CompletionFilter.Filter(items, ""));
    }
}
