using Xunit;

namespace Squill.App.WinUI.Tests;

public class LongPasteUiTests
{
    private static string Read(string name) => File.ReadAllText(Path.Combine("Xaml", name));

    [Theory]
    [InlineData("IsIndeterminate = true")]
    [InlineData("results.pasting")]
    [InlineData("Tokens.Brush")]
    public void The_busy_overlay_shows_an_indeterminate_bar_with_token_brushes(string expected) => Assert.Contains(expected, Read("BusyOverlay.cs"));

    [Fact]
    public void The_busy_overlay_looks_up_no_resource_and_binds_no_items_source()
    {
        var code = Read("BusyOverlay.cs");
        Assert.DoesNotContain("ItemsSource", code);
        Assert.DoesNotContain("Application.Current.Resources", code);
    }

    [Fact]
    public void The_grid_paste_is_asynchronous_when_long()
    {
        var code = Read("DataGridCanvas.cs");
        Assert.Contains("LongPaste.IsLong", code);
        Assert.Contains("PasteAsync(", code);
    }

    [Theory]
    [InlineData("DataGridView.cs")]
    [InlineData("CellPreviewPanel.cs")]
    public void The_text_boxes_handle_the_paste_event(string file)
    {
        var code = Read(file);
        Assert.Contains(".Paste +=", code);
        Assert.Contains("SelectedText = ", code);
    }

    [Fact]
    public void The_preview_pane_shows_a_working_strip()
    {
        var code = Read("CellPreviewPanel.cs");
        Assert.Contains("IsWorking", code);
        Assert.Contains("preview.working", code);
    }
}
