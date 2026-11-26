using Xunit;

namespace Squill.App.WinUI.Tests;

public class AutoRefreshButtonTests
{
    private static string Read(string file) => File.ReadAllText(Path.Combine("Xaml", file));

    [Fact]
    public void Menu_is_built_in_code_from_the_view_model_presets()
    {
        var code = Read("AutoRefreshMenu.cs");
        foreach (var text in new[] { "RadioMenuFlyoutItem", "MenuFlyoutSeparator", "SetAutoRefreshCommand", "SetCustomAutoRefreshCommand", "AutoRefreshPresets", "query.autoRefresh.custom", "Opening" })
            Assert.Contains(text, code);
        Assert.DoesNotContain("ItemsSource", code);
        Assert.DoesNotContain("Application.Current.Resources", code);
    }

    [Fact]
    public void Toolbar_puts_the_clock_before_stop_and_follows_the_view_model()
    {
        var code = Read("QueryTabView.cs");
        foreach (var text in new[] { "_autoRefresh, _stop", "AutoRefreshToolTip", "ResultsReplacing", "ResultsReplaced", "IsRefreshing", "SaveView", "RestoreView" })
            Assert.Contains(text, code);
    }

    [Fact]
    public void Grid_can_save_and_restore_its_view()
    {
        var code = Read("DataGridCanvas.cs");
        Assert.Contains("SaveView", code);
        Assert.Contains("RestoreView", code);
        Assert.Contains("GridViewMemo", code);
    }

    [Fact]
    public void Timer_icon_exists() => Assert.Contains("SquillIconTimer", File.ReadAllText(Path.Combine("Styles", "Icons.xaml")));
}
