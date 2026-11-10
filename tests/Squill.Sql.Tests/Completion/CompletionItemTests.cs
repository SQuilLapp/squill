using Squill.Core;
using Squill.Sql.Completion;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class CompletionItemTests
{
    private static readonly TextRange Range = new(0, 0);

    [Fact]
    public void NewMembersDefault()
    {
        var item = new CompletionItem("a", CompletionKind.Keyword, "a", null, null, Range);
        Assert.Null(item.FilterText);
        Assert.Null(item.Documentation);
        Assert.False(item.IsSnippet);
    }

    [Fact]
    public void WithKeepsNewMembers()
    {
        var item = new CompletionItem("a", CompletionKind.Keyword, "a", null, null, Range, "f", "doc", true);
        var copy = item with { Label = "b" };
        Assert.Equal("f", copy.FilterText);
        Assert.Equal("doc", copy.Documentation);
        Assert.True(copy.IsSnippet);
    }

    [Fact]
    public void FilterMatchesFilterText()
    {
        var item = new CompletionItem("orders o", CompletionKind.Struct, "orders o", null, null, Range, "orders");
        var items = new[] { item };
        Assert.Single(CompletionFilter.Filter(items, "ord"));
        Assert.Empty(CompletionFilter.Filter(items, "o "));
    }

    [Fact]
    public void FilterFallsBackToLabel()
    {
        var item = new CompletionItem("ORDER BY", CompletionKind.Keyword, "ORDER BY", null, null, Range);
        Assert.Single(CompletionFilter.Filter([item], "order b"));
    }

    [Theory]
    [InlineData("a$b")]
    [InlineData("x}y")]
    [InlineData("c\\d")]
    public void EscapeRoundTrips(string literal)
    {
        Assert.Equal(literal, SnippetText.ToPlain(SnippetText.Escape(literal)));
    }

    [Fact]
    public void EscapeEscapesSpecials()
    {
        Assert.Equal("a\\$b\\}c\\\\d", SnippetText.Escape("a$b}c\\d"));
    }

    [Fact]
    public void ToPlainRemovesTabStops()
    {
        Assert.Equal("SELECT cols FROM table", SnippetText.ToPlain("SELECT ${1:cols} FROM ${2:table}$0"));
        Assert.Equal("a b", SnippetText.ToPlain("a $1b"));
    }
}
