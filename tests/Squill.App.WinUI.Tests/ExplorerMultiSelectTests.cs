using Xunit;

namespace Squill.App.WinUI.Tests;

public class ExplorerMultiSelectTests
{
    private static string Read(string file) => File.ReadAllText(Path.Combine("Xaml", file));

    [Fact]
    public void Explorer_tree_allows_several_selected_nodes() =>
        Assert.Contains("x:Name=\"Tree\" Grid.Row=\"1\" SelectionMode=\"Multiple\"", Read("SidebarView.xaml"));

    [Fact]
    public void Explorer_tree_handles_keys_on_the_tree_not_on_the_filter()
    {
        var text = Read("SidebarView.xaml");
        Assert.Contains("KeyDown=\"OnTreeKeyDown\"", text);
        Assert.DoesNotContain("x:Name=\"Filter\" KeyDown", text);
    }

    [Fact]
    public void Explorer_rows_use_the_style_without_selection_checkbox()
    {
        Assert.Contains("BasedOn=\"{StaticResource ExplorerTreeViewItemStyle}\"", Read("SidebarView.xaml"));
        var styles = File.ReadAllText(Path.Combine("Styles", "Controls.xaml"));
        Assert.DoesNotContain("MultiSelectCheckBox.Visibility", styles);
    }
}
