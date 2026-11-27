using Xunit;

namespace Squill.App.WinUI.Tests;

public class CellPreviewPanelTests
{
    [Fact]
    public void Sync_ComparesNormalizedText() =>
        Assert.Contains("NormalizeLineBreaks(", File.ReadAllText(Path.Combine("Xaml", "CellPreviewPanel.cs")));

    [Fact]
    public void Pane_paints_from_its_own_theme()
    {
        var text = File.ReadAllText(Path.Combine("Xaml", "CellPreviewPanel.cs"));
        foreach (var needle in new[] { "ActualThemeChanged", "Loaded", "Tokens.Brush(this, \"SquillBgSurfaceBrush\")", "Tokens.Brush(this, \"SquillFgBrush\")" })
            Assert.Contains(needle, text);
        Assert.DoesNotContain("Tokens.Brush(key)", text);
        Assert.DoesNotContain("Tokens.Brush(\"", text);
    }

    [Fact]
    public void Pane_caps_and_copies_whole_value()
    {
        var text = File.ReadAllText(Path.Combine("Xaml", "CellPreviewPanel.cs"));
        foreach (var needle in new[] { "IsTruncated", "CopyingToClipboard", "FullValue", "Notice", "FormatLabel", "Tokens.Brush(this, \"SquillFg3Brush\")" })
            Assert.Contains(needle, text);
    }
}
