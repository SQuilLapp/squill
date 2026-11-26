using Avalonia.Controls;
using Avalonia.VisualTree;
using Squill.App.Avalonia.Controls;
using Squill.App.Avalonia.Views;
using Squill.Presentation.Editing;
using Squill.Presentation.Localization;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class LongPasteUiTests
{
    private static string Source(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir.FullName, "Squill.slnx")))
        {
            dir = dir.Parent ?? throw new DirectoryNotFoundException("Squill.slnx");
        }

        return File.ReadAllText(Path.Combine([dir.FullName, "src", "Squill.App.Avalonia", .. parts]));
    }

    private static Task RunAsync(Action body) => Headless.Session.Dispatch(body, TestContext.Current.CancellationToken);

    [Fact]
    public Task The_overlay_stays_visible_until_the_last_hide() => RunAsync(() =>
    {
        var overlay = new BusyOverlay();
        Assert.False(overlay.IsVisible);
        overlay.Show();
        overlay.Show();
        overlay.Hide();
        Assert.True(overlay.IsVisible);
        overlay.Hide();
        Assert.False(overlay.IsVisible);
        overlay.Hide();
        Assert.False(overlay.IsVisible);
    });

    [Fact]
    public Task The_overlay_says_pasting_with_an_indeterminate_bar() => RunAsync(() =>
    {
        var overlay = new BusyOverlay();
        var window = new Window { Content = overlay };
        window.Show();
        overlay.Show();
        var texts = overlay.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains(UiText.Get("results.pasting"), texts);
        Assert.Contains(overlay.GetVisualDescendants().OfType<ProgressBar>(), b => b.IsIndeterminate);
        window.Close();
    });

    [Theory]
    [InlineData("Controls", "DataGridCanvas.cs", "LongPaste.IsLong")]
    [InlineData("Controls", "DataGridCanvas.cs", "BusyChanged")]
    [InlineData("Controls", "DataGridView.cs", "LongPaste.IsLong")]
    [InlineData("Controls", "DataGridView.cs", "BusyChanged")]
    [InlineData("Views", "CellPreviewPanel.cs", "PastingFromClipboard")]
    [InlineData("Views", "CellPreviewPanel.cs", "SelectedText = ")]
    [InlineData("Views", "CellPreviewPanel.cs", "IsWorking")]
    [InlineData("Views", "CellPreviewPanel.cs", "preview.working")]
    [InlineData("Views", "QueryTabView.axaml", "BusyOverlay")]
    public void The_paste_paths_are_wired(string folder, string file, string expected) => Assert.Contains(expected, Source(folder, file));
}
