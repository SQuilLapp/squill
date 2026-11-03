using Squill.App.WinUI.Views;
using Squill.Core.Design;
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
}
