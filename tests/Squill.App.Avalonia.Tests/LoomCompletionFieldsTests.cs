using LoomEditor.Core.Languages.Features;
using Squill.Core;
using Squill.Sql.Completion;
using Xunit;
using LoomCompletionItem = LoomEditor.Core.Languages.Features.CompletionItem;
using SqlCompletionItem = Squill.Sql.Completion.CompletionItem;

namespace Squill.App.Avalonia.Tests;

public sealed class LoomCompletionFieldsTests
{
    private static LoomCompletionItem Apply(SqlCompletionItem source)
    {
        var target = new LoomCompletionItem(source.Label, source.InsertText, CompletionItemKind.Text);
        LoomSqlProviders.ApplyFields(target, source);
        return target;
    }

    [Fact]
    public void ApplyFields_CopiesFilterTextDocumentationAndSnippetRule()
    {
        var item = new SqlCompletionItem("orders o", CompletionKind.Struct, "orders o", "ON x", "00", new TextRange(0, 0), "orders", "**doc**", true);
        var target = Apply(item);
        Assert.Equal("orders", target.FilterText);
        Assert.Equal("**doc**", target.Documentation?.Value);
        Assert.Equal(InsertTextRules.InsertAsSnippet, target.InsertTextRules);
        Assert.Equal("ON x", target.Detail);
        Assert.Equal("00", target.SortText);
    }

    [Fact]
    public void ApplyFields_DefaultsAreNullAndNone()
    {
        var target = Apply(new SqlCompletionItem("a", CompletionKind.Keyword, "a", null, null, new TextRange(0, 0)));
        Assert.Null(target.FilterText);
        Assert.Null(target.Documentation);
        Assert.Equal(InsertTextRules.None, target.InsertTextRules);
    }
}
