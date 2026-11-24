using Xunit;

namespace Squill.App.WinUI.Tests;

public class GridCellPreviewTests
{
    private static string Canvas => File.ReadAllText(Path.Combine("Xaml", "DataGridCanvas.cs"));

    [Fact]
    public void Draw_and_measure_use_the_single_line_preview()
    {
        var code = Canvas;
        Assert.True(code.Split("CellText.Preview(").Length - 1 >= 2);
    }

    [Fact]
    public void Overflowing_cells_trim_with_an_ellipsis_and_never_wrap()
    {
        var code = Canvas;
        Assert.Contains("CanvasTrimmingSign.Ellipsis", code);
        Assert.Contains("CanvasTextTrimmingGranularity.Character", code);
        Assert.Contains("WordWrapping = CanvasWordWrapping.NoWrap", code);
    }
}
