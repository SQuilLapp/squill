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

    private static readonly string[] NewColors =
    [
        "env-prod-soft", "env-staging-soft", "env-dev-soft", "env-local-soft",
        "engine-postgres", "engine-mysql", "engine-sqlserver", "engine-sqlite",
    ];

    [Fact]
    public void New_color_tokens_exist_in_both_themes_and_both_outputs()
    {
        var t = Tokens;
        var winUi = XamlGenerator.WinUi(t);
        var avalonia = XamlGenerator.Avalonia(t);
        foreach (var name in NewColors)
        {
            Assert.True(t.Themes["light"].ContainsKey(name) && t.Themes["dark"].ContainsKey(name), name);
            var pascal = XamlGenerator.Pascal(name);
            Assert.Contains($"x:Key=\"Squill{pascal}Color\"", winUi);
            Assert.Contains($"x:Key=\"Squill{pascal}Brush\"", winUi);
            Assert.Contains($"x:Key=\"Squill{pascal}Brush\"", avalonia);
        }
    }

    [Fact]
    public void Typography_and_space_resources_are_emitted_in_both_outputs()
    {
        var t = Tokens;
        foreach (var xml in new[] { XamlGenerator.WinUi(t), XamlGenerator.Avalonia(t) })
        {
            Assert.Contains("x:Key=\"SquillFontSizeBody\">13<", xml);
            Assert.Contains("x:Key=\"SquillLineHeightMonoEditor\">20<", xml);
            Assert.Contains("x:Key=\"SquillFontWeightBodyStrong\">SemiBold<", xml);
            Assert.Contains("x:Key=\"SquillFontWeightBody\">Normal<", xml);
            Assert.Contains("x:Key=\"SquillSpace3\">8<", xml);
            Assert.Contains("x:Key=\"SquillSpace0\">0<", xml);
            foreach (var name in t.Metrics.Typography.Keys)
                Assert.Contains($"x:Key=\"SquillFontSize{XamlGenerator.Pascal(name)}\"", xml);
        }
    }

    [Fact]
    public void Shadows_are_emitted_for_avalonia_only()
    {
        var t = Tokens;
        var avalonia = XamlGenerator.Avalonia(t);
        Assert.Contains("x:Key=\"SquillShadow3\">0 18 48 0 #2E0A0C1E<", avalonia);
        Assert.Contains("x:Key=\"SquillShadow2\">0 4 14 0 #59000000<", avalonia);
        Assert.DoesNotContain("Shadow", XamlGenerator.WinUi(t));
    }
}
