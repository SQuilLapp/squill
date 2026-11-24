using Xunit;
using Squill.Presentation.Results;

namespace Squill.Presentation.Tests.Results;

public sealed class CellTextTests
{
    [Fact]
    public void Preview_ShortPlainText_ReturnsSameInstance()
    {
        var s = new string('a', 10);
        Assert.Same(s, CellText.Preview(s));
    }

    [Fact]
    public void Preview_PlainSpaces_Kept()
    {
        var s = "a   b";
        Assert.Same(s, CellText.Preview(s));
    }

    [Fact]
    public void Preview_LineBreaks_BecomeOneSpace() => Assert.Equal("a b c d", CellText.Preview("a\r\nb\nc\rd"));

    [Fact]
    public void Preview_IndentedJson_ReadsAsOneLine() =>
        Assert.Equal("{ \"type\": \"Point\", \"coordinates\": [ 2.63, 48.99 ] }",
            CellText.Preview("{\n  \"type\": \"Point\",\n  \"coordinates\": [\n    2.63,\n    48.99\n  ]\n}"));

    [Fact]
    public void Preview_TabsAndControlChars_BecomeOneSpace() => Assert.Equal("a b c d e", CellText.Preview("a\tb\u0001\u0085c\u2028d\u2029e"));

    [Fact]
    public void Preview_ExactlyPreviewLength_NoEllipsis()
    {
        var s = new string('a', CellText.PreviewLength);
        Assert.Equal(s, CellText.Preview(s));
    }

    [Fact]
    public void Preview_LongText_CappedWithEllipsis()
    {
        var p = CellText.Preview(new string('a', CellText.PreviewLength + 1));
        Assert.Equal(CellText.PreviewLength, p.Length);
        Assert.Equal('…', p[^1]);
    }

    [Fact]
    public void Preview_SurrogatePairAtCut_NotSplit()
    {
        var s = new string('a', CellText.PreviewLength - 2) + "\U0001F600" + "zzz";
        var p = CellText.Preview(s);
        Assert.Equal('…', p[^1]);
        Assert.False(char.IsHighSurrogate(p[^2]));
        Assert.Equal(CellText.PreviewLength - 2, p.Length - 1);
    }

    [Fact]
    public void Preview_MillionLineBreaksThenText_StopsEarly()
    {
        var p = CellText.Preview("a" + new string('\n', 1_000_000) + "b");
        Assert.True(p.Length <= CellText.PreviewLength);
        Assert.Equal('…', p[^1]);
    }

    [Fact]
    public void Preview_TenMegabytes_IsCapped()
    {
        var p = CellText.Preview(new string('x', 10_000_000));
        Assert.Equal(CellText.PreviewLength, p.Length);
        Assert.Equal('…', p[^1]);
    }

    [Fact]
    public void IsSingleLine_PlainText_True() => Assert.True(CellText.IsSingleLine("abc {\"a\": 1}"));

    [Fact]
    public void IsSingleLine_Tab_True() => Assert.True(CellText.IsSingleLine("a\tb"));

    [Theory]
    [InlineData("a\rb")]
    [InlineData("a\nb")]
    [InlineData("a\r\nb")]
    [InlineData("a\u000Bb")]
    [InlineData("a\u000Cb")]
    [InlineData("a\u0085b")]
    [InlineData("a\u2028b")]
    [InlineData("a\u2029b")]
    public void IsSingleLine_LineBreak_False(string text) => Assert.False(CellText.IsSingleLine(text));
}
