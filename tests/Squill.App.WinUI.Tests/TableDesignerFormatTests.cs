using Squill.App.WinUI.Views;
using Squill.Core.Design;
using System.Xml.Linq;
using Xunit;

namespace Squill.App.WinUI.Tests;

public class TableDesignerFormatTests
{
    [Theory]
    [InlineData(DesignIssueSeverity.Error, true)]
    [InlineData(DesignIssueSeverity.Warning, false)]
    public void Issue_badge_follows_the_severity(DesignIssueSeverity severity, bool isError)
    {
        Assert.Equal(isError, TableDesignerFormat.IsError(severity));
        Assert.Equal(!isError, TableDesignerFormat.IsWarning(severity));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("2 modifications en brouillon", true)]
    public void Draft_state_is_drawn_only_when_there_is_text(string? text, bool drawn) =>
        Assert.Equal(drawn, TableDesignerFormat.HasDraft(text));

    [Fact]
    public void Cell_box_style_has_a_foreground_and_its_own_template_that_never_forces_the_light_theme()
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var style = XDocument.Load(Path.Combine("Xaml", "TableDesignerView.xaml")).Descendants()
            .Single(e => (string?)e.Attribute(x + "Key") == "CellBoxStyle");
        Assert.Contains(style.Elements(), e => (string?)e.Attribute("Property") == "Foreground");
        Assert.Contains(style.Elements(), e => (string?)e.Attribute("Property") == "Template");
        Assert.DoesNotContain("RequestedTheme", style.ToString());
    }
}
