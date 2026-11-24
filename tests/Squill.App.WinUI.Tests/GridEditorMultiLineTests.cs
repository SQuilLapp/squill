using Xunit;

namespace Squill.App.WinUI.Tests;

public class GridEditorMultiLineTests
{
    private static string View => File.ReadAllText(Path.Combine("Xaml", "DataGridView.cs"));

    [Theory]
    [InlineData(".Paste +=")]
    [InlineData("PreviewKeyDown")]
    [InlineData("PasteText(")]
    [InlineData("EditDisplayText")]
    [InlineData("IsMultiLine")]
    [InlineData("IsReadOnly")]
    [InlineData("results.multiLineEdit")]
    public void The_editor_hands_multi_line_text_to_the_controller(string expected) => Assert.Contains(expected, View);

    [Fact]
    public void The_editor_stays_one_line_and_looks_up_no_brush_resource()
    {
        var code = View;
        Assert.DoesNotContain("AcceptsReturn = true", code);
        Assert.DoesNotContain("Application.Current.Resources", code);
    }
}
