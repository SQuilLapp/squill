using Squill.DesignSystem;
using Xunit;

namespace Squill.DesignSystem.Tests;

public class XamlGeneratorTests
{
    private static TokenSet Tokens => XamlGenerator.Load(File.ReadAllText("squill.tokens.json"));

    [Theory]
    [InlineData("#9488F5", "#FF9488F5")]
    [InlineData("rgba(148,136,245,.16)", "#299488F5")]
    public void Argb_converts_css_colors(string css, string expected) =>
        Assert.Equal(expected, XamlGenerator.Argb(css));

    [Fact]
    public void Both_themes_define_the_same_keys() =>
        Assert.Equal(Tokens.Themes["dark"].Keys.Order(), Tokens.Themes["light"].Keys.Order());

    [Fact]
    public void WinUi_output_has_accent_and_both_themes()
    {
        var xml = XamlGenerator.WinUi(Tokens);
        Assert.Contains("x:Key=\"SquillAccentColor\">#FF9488F5", xml);
        Assert.Contains("x:Key=\"SquillAccentColor\">#FF5B4CD6", xml);
        Assert.Contains("SquillRowHeightStandard\">26", xml);
    }

    [Fact]
    public void Avalonia_output_has_dark_and_light() =>
        Assert.Contains("x:Key=\"Light\"", XamlGenerator.Avalonia(Tokens));
}
