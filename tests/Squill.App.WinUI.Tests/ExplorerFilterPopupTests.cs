using Xunit;

namespace Squill.App.WinUI.Tests;

public class ExplorerFilterPopupTests
{
    private static string Read(string file) => File.ReadAllText(Path.Combine("Xaml", file));

    [Fact]
    public void Row_template_binds_the_badge_and_a_one_way_trailing_text()
    {
        var xaml = Read("SidebarView.xaml");
        Assert.Contains("FilterBadge, Mode=OneWay", xaml);
        Assert.Contains("Click=\"OnBadgeClicked\"", xaml);
        Assert.Contains("Text=\"{x:Bind Trailing, Mode=OneWay}\"", xaml);
    }

    [Fact]
    public void Popup_is_built_in_code_without_items_source_or_global_resources()
    {
        var code = Read("ExplorerFilterMenu.cs");
        Assert.DoesNotContain("ItemsSource", code);
        Assert.DoesNotContain("Application.Current.Resources", code);
    }

    [Fact]
    public void Context_menu_offers_the_popup_instead_of_the_table_picker()
    {
        var code = Read("SidebarView.xaml.cs");
        Assert.DoesNotContain("explorer.chooseTables", code);
        Assert.Contains("explorer.chooseShown", code);
    }

    [Fact]
    public void Window_no_longer_installs_the_table_picker() => Assert.DoesNotContain("WinUiTablePicker", Read("MainWindow.xaml.cs"));

    [Fact]
    public void Badge_button_style_exists() => Assert.Contains("x:Key=\"ExplorerBadgeButtonStyle\"", File.ReadAllText(Path.Combine("Styles", "Controls.xaml")));

    [Fact]
    public void Popup_is_resizable_and_placed_inside_the_window()
    {
        var menu = Read("ExplorerFilterMenu.cs");
        foreach (var text in new[] { "ResizeGrip", "ExplorerFilterPopupSize.Fit", "OpensAbove", "TopEdgeAlignedLeft", "PositiveInfinity" })
            Assert.Contains(text, menu);
        Assert.DoesNotContain("MaxHeight = ListHeight", menu);
        var grip = Read("ResizeGrip.cs");
        Assert.Contains("SizeNorthwestSoutheast", grip);
        Assert.Contains("CapturePointer", grip);
        foreach (var code in new[] { menu, grip })
        {
            Assert.DoesNotContain("ItemsSource", code);
            Assert.DoesNotContain("Application.Current.Resources", code);
        }
    }
}
