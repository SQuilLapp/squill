using Squill.DesignSystem;
using Xunit;

namespace Squill.App.WinUI.Tests;

public class TokensTests
{
    [Fact]
    public void Committed_tokens_match_the_generator()
    {
        var expected = XamlGenerator.WinUi(XamlGenerator.Load(File.ReadAllText("squill.tokens.json")));
        Assert.Equal(Normalize(expected), Normalize(File.ReadAllText("SquillTokens.xaml")));
    }

    private static string Normalize(string text) => text.ReplaceLineEndings("\n");
}
