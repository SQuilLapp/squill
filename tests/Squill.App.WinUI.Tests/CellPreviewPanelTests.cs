using Xunit;

namespace Squill.App.WinUI.Tests;

public class CellPreviewPanelTests
{
    [Fact]
    public void Sync_ComparesNormalizedText() =>
        Assert.Contains("NormalizeLineBreaks(", File.ReadAllText(Path.Combine("Xaml", "CellPreviewPanel.cs")));
}
