using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Squill.App.WinUI.Tests;

public class XamlHandlerTests
{
    [Theory]
    [InlineData("MainWindow")]
    [InlineData("SidebarView")]
    [InlineData("HistoryView")]
    [InlineData("SnippetsView")]
    public void Every_event_handler_named_in_xaml_exists_in_the_code_behind(string view)
    {
        var xaml = XDocument.Load($"Xaml/{view}.xaml");
        var code = File.ReadAllText($"Xaml/{view}.xaml.cs");
        var handlers = xaml.Descendants().SelectMany(e => e.Attributes())
            .Where(a => a.Name.LocalName is "Click" or "Invoked" or "TextChanged" or "DoubleTapped" or "KeyDown"
                or "SelectionChanged" or "TabCloseRequested" or "Checked")
            .Select(a => a.Value)
            .ToList();

        Assert.NotEmpty(handlers);
        foreach (var handler in handlers)
            Assert.Matches(new Regex($@"\b{handler}\("), code);
    }
}
