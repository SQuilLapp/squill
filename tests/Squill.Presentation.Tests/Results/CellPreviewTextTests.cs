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

    private static string Lines(int count, string eol) => string.Join(eol, Enumerable.Repeat("a", count));

    [Fact]
    public void Head_WithinLimits_ReturnsNull()
    {
        Assert.Null(CellPreviewViewModel.Head(""));
        Assert.Null(CellPreviewViewModel.Head(Lines(1_999, "\n")));
        Assert.Null(CellPreviewViewModel.Head(Lines(CellPreviewViewModel.MaxShownLines, "\n")));
        Assert.Null(CellPreviewViewModel.Head(new string('a', CellPreviewViewModel.MaxShownChars)));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void Head_TooManyLines_CutsAtLineBreak(string eol)
    {
        var head = CellPreviewViewModel.Head(Lines(2_500, eol));

        Assert.Equal(Lines(CellPreviewViewModel.MaxShownLines, eol), head);
    }

    [Fact]
    public void Head_LongSingleLine_CutsAtMaxChars() =>
        Assert.Equal(CellPreviewViewModel.MaxShownChars, CellPreviewViewModel.Head(new string('a', 1_700_000))!.Length);

    [Fact]
    public void Head_DoesNotSplitSurrogatePair()
    {
        var text = new string('a', CellPreviewViewModel.MaxShownChars - 1) + "\U0001F600" + "tail";

        var head = CellPreviewViewModel.Head(text)!;

        Assert.Equal(CellPreviewViewModel.MaxShownChars - 1, head.Length);
        Assert.False(char.IsHighSurrogate(head[^1]));
    }

    [Fact]
    public void Head_LongLinesBeforeLineLimit_CutsAtChars() =>
        Assert.Equal(CellPreviewViewModel.MaxShownChars, CellPreviewViewModel.Head(string.Join('\n', Enumerable.Repeat(new string('a', 199), 1_000)))!.Length);
}
