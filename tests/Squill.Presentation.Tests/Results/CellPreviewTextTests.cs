using Squill.Presentation.Results;
using Xunit;

namespace Squill.Presentation.Tests.Results;

public sealed class CellPreviewTextTests
{
    [Theory]
    [InlineData("a\r\nb", "a\nb")]
    [InlineData("a\rb", "a\nb")]
    [InlineData("a\r\r\nb", "a\n\nb")]
    [InlineData("a\n\rb", "a\n\nb")]
    [InlineData("a\r", "a\n")]
    [InlineData("\r\n\r\n", "\n\n")]
    public void NormalizeLineBreaks_LineBreakForms_BecomeLf(string text, string expected) =>
        Assert.Equal(expected, CellPreviewViewModel.NormalizeLineBreaks(text));

    [Theory]
    [InlineData("")]
    public void NormalizeLineBreaks_NoCr_ReturnsSameInstance(string text) =>
        Assert.Same(text, CellPreviewViewModel.NormalizeLineBreaks(text));

    [Fact]
    public void NormalizeLineBreaks_UnicodeSeparatorsAndTabs_AreKept()
    {
        var text = "a" + (char)0x2028 + "b	c" + (char)0x85 + "d" + (char)0x2029;
        Assert.Same(text, CellPreviewViewModel.NormalizeLineBreaks(text));
    }

    [Fact]
    public void TryPretty_Indented_UsesLfOnly()
    {
        Assert.True(CellPreviewViewModel.TryPretty("{\"a\":{\"b\":[1,2]}}", out var pretty, out _));
        Assert.Contains('\n', pretty);
        Assert.DoesNotContain('\r', pretty);
    }
}
