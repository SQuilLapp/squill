using Squill.Core;
using Squill.Presentation.Localization;
using Squill.Presentation.Palette;
using Xunit;

namespace Squill.Presentation.Tests.Palette;

public sealed class PaletteLineProviderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (PaletteLineProvider Provider, PaletteHistoryProviderTests.FakeEditor? Editor) Make(string? text)
    {
        var editor = text is null ? null : new PaletteHistoryProviderTests.FakeEditor(text);
        return (new PaletteLineProvider(new PaletteHistoryProviderTests.FakeHost { Editor = editor }), editor);
    }

    [Theory]
    [InlineData("a\nb\nc", "2", 2)]
    [InlineData("a\r\nb\r\nc", "3", 6)]
    [InlineData("a\rb\rc", "2", 2)]
    [InlineData("a\nb", "99", 2)]
    [InlineData("a\nb", "0", 0)]
    public async Task Run_Number_MovesCaretToLineStartClamped(string text, string term, int caret)
    {
        var (provider, editor) = Make(text);
        editor!.Selection = new TextRange(0, 1);

        var item = Assert.Single(provider.Search(term));
        await item.Run(PaletteRunMode.Default, Ct);

        Assert.True(item.IsEnabled);
        Assert.Equal(caret, editor.CaretOffset);
        Assert.Equal(0, editor.Selection.Length);
    }

    [Fact]
    public void Search_Number_ShowsTitleAndTotal()
    {
        using var scope = UiText.Override(UiLanguage.French);
        var (provider, _) = Make("a\nb\nc");

        var item = Assert.Single(provider.Search("2"));

        Assert.Equal("Aller à la ligne 2", item.Title);
        Assert.Equal("3 lignes", item.Detail);
    }

    [Fact]
    public void Search_NoEditor_IsDisabled()
    {
        var (provider, _) = Make(null);

        Assert.False(Assert.Single(provider.Search("5")).IsEnabled);
    }

    [Fact]
    public void Search_NotANumber_ReturnsDisabledHint()
    {
        using var scope = UiText.Override(UiLanguage.French);
        var (provider, _) = Make("a");

        var item = Assert.Single(provider.Search("abc"));

        Assert.False(item.IsEnabled);
        Assert.Equal("Saisissez un numéro de ligne", item.Title);
    }
}
