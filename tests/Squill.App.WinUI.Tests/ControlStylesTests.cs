using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Squill.App.WinUI.Tests;

public partial class ControlStylesTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Theory]
    [InlineData("Controls.xaml")]
    [InlineData("ControlResources.xaml")]
    public void Control_styles_contain_no_literal_color_or_font(string file)
    {
        var text = File.ReadAllText(Path.Combine("Styles", file));
        Assert.DoesNotMatch(HexColor(), text);
        Assert.DoesNotMatch(FontName(), text);
        // Named colors other than Transparent would bypass the tokens.
        foreach (Match m in ColorAttribute().Matches(text))
            Assert.Equal("Transparent", m.Groups[1].Value);
    }

    [Theory]
    [InlineData("ControlResources.xaml")]
    [InlineData("Controls.xaml")]
    public void Every_brush_comes_from_a_token(string file)
    {
        var tokens = TokenKeys();
        var text = File.ReadAllText(Path.Combine("Styles", file));
        foreach (Match m in ThemeRef().Matches(text))
            Assert.Contains(m.Groups[1].Value, tokens);
    }

    [Fact]
    public void Resource_keys_exist_once_and_both_themes_get_them()
    {
        var resources = XDocument.Load(Path.Combine("Styles", "ControlResources.xaml")).Root!;
        var keys = resources.Elements().Select(e => (string?)e.Attribute(X + "Key")).Where(k => k is not null).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        foreach (var key in new[] { "ButtonBackground", "AccentButtonBackground", "TextControlBorderBrushFocused", "CheckBoxCheckBackgroundFillChecked",
                     "ToggleSwitchFillOn", "ListViewItemBackgroundSelected", "MenuFlyoutPresenterBackground", "ToolTipBackground",
                     "ScrollBarThumbFill", "ProgressBarForeground", "ContentDialogBackground", "SystemControlFocusVisualPrimaryBrush", "AccentFillColorDefaultBrush" })
            Assert.Contains(key, keys);

        var controls = XDocument.Load(Path.Combine("Styles", "Controls.xaml")).Root!;
        var themes = controls.Element(controls.Name.Namespace + "ResourceDictionary.ThemeDictionaries")!.Elements()
            .Select(e => (string?)e.Attribute(X + "Key")).ToList();
        Assert.Equal(["Default", "Light"], themes);
    }

    [Fact]
    public void Shared_styles_and_pieces_are_declared()
    {
        var controls = XDocument.Load(Path.Combine("Styles", "Controls.xaml")).Root!;
        var keys = controls.Elements().Select(e => (string?)e.Attribute(X + "Key")).ToHashSet();
        foreach (var key in new[] { "PrimaryButtonStyle", "OutlineSuccessButtonStyle", "OutlineDangerButtonStyle", "GhostButtonStyle", "IconButtonStyle", "VariantButtonStyle",
                     "SegmentedRadioButtonStyle", "KeyboardChipStyle", "BadgeStyle", "BadgeInfoStyle", "BadgeSuccessStyle", "BadgeWarningStyle",
                     "BadgeDangerStyle", "BadgeEnvProdStyle", "EngineBadgeStyle", "EngineBadgePostgresStyle", "SquillTabItemStyle",
                     "SquillTabItemProdStyle", "EnvSquareStyle", "EnvSquareLocalStyle", "TabDirtyDotStyle" })
            Assert.Contains(key, keys);
    }

    private static HashSet<string> TokenKeys() =>
        XDocument.Load("SquillTokens.xaml").Descendants().Select(e => (string?)e.Attribute(X + "Key")).OfType<string>()
            .Select(k => k).ToHashSet();

    [GeneratedRegex("#[0-9A-Fa-f]{6,8}\\b")]
    private static partial Regex HexColor();

    [GeneratedRegex("(Segoe|\\bInter\\b|JetBrains|Consolas|Cascadia)", RegexOptions.IgnoreCase)]
    private static partial Regex FontName();

    [GeneratedRegex("Color=\"([A-Za-z]+)\"")]
    private static partial Regex ColorAttribute();

    [GeneratedRegex("ThemeResource (Squill\\w+)\\}")]
    private static partial Regex ThemeRef();
}
