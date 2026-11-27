using Xunit;

namespace Squill.App.WinUI.Tests;

public class DefinitionKeysTests
{
    private static string Read(string file) => File.ReadAllText(Path.Combine("Xaml", file));

    [Fact]
    public void Query_tab_binds_the_definition_and_usage_keys()
    {
        var code = Read("QueryTabView.cs");
        foreach (var text in new[] { "VirtualKey.F12", "VirtualKey.F7", "VirtualKey.B", "VirtualKey.Escape", "VirtualKey.Menu", "GoToDefinitionCommand", "GoToDefinitionInDesignerCommand", "FindUsagesCommand", "ClearHighlightsCommand", "DefinitionRequested" })
            Assert.Contains(text, code);
    }

    [Fact]
    public void Editor_paints_usages_with_a_capped_decoration_layer_and_reports_ctrl_click()
    {
        var code = Read("LoomSqlEditor.cs");
        foreach (var text in new[] { "SetHighlights", "MaxUsageHighlights = 1000", "CreateDecorationsCollection", "DarkBackground", "ClassName = UsageClass", "PointerPressedEvent", "DefinitionRequested" })
            Assert.Contains(text, code);
        Assert.DoesNotContain("Application.Current.Resources", code);
    }

    [Fact]
    public void Sidebar_shows_a_selection_made_by_code()
    {
        var code = Read("SidebarView.xaml.cs");
        Assert.Contains("OnExplorerPropertyChanged", code);
        Assert.Contains("StartBringIntoView", code);
    }
}
