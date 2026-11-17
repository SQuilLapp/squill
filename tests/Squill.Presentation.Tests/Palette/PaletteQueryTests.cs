using Squill.Presentation.Palette;
using Xunit;

namespace Squill.Presentation.Tests.Palette;

public sealed class PaletteQueryTests
{
    [Theory]
    [InlineData(">  run", PaletteMode.Actions, "run", ">")]
    [InlineData("#orders", PaletteMode.Objects, "orders", "#")]
    [InlineData(":12", PaletteMode.Line, "12", ":")]
    [InlineData("!select", PaletteMode.History, "select", "!")]
    [InlineData("plain", PaletteMode.All, "plain", "")]
    [InlineData("", PaletteMode.All, "", "")]
    public void Parse_Text_ModeTermPrefix(string text, PaletteMode mode, string term, string prefix) =>
        Assert.Equal((mode, term, prefix), PaletteQuery.Parse(text));

    [Fact]
    public void Parse_At_IsPlainText() =>
        Assert.Equal((PaletteMode.All, "@x", ""), PaletteQuery.Parse("@x"));

    [Theory]
    [InlineData(PaletteMode.All)]
    [InlineData(PaletteMode.Actions)]
    [InlineData(PaletteMode.Objects)]
    [InlineData(PaletteMode.Line)]
    [InlineData(PaletteMode.History)]
    public void WithMode_RoundTrips_KeepsTerm(PaletteMode mode)
    {
        string text = PaletteQuery.WithMode("#orders", mode);
        (PaletteMode m, string term, _) = PaletteQuery.Parse(text);
        Assert.Equal(mode, m);
        Assert.Equal("orders", term);
    }

    [Fact]
    public void TryLine_Digits_Parses()
    {
        Assert.True(PaletteQuery.TryLine("12", out int line));
        Assert.Equal(12, line);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("1a")]
    [InlineData("-3")]
    public void TryLine_NotDigits_Fails(string term) => Assert.False(PaletteQuery.TryLine(term, out _));
}
