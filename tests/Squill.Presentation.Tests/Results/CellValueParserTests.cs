using Xunit;
using Squill.Core;
using Squill.Presentation.Results;

namespace Squill.Presentation.Tests.Results;

public sealed class CellValueParserTests
{
    private static ColumnInfo Col(Type t) => new("c", "x", t, true, 0);

    private static object? Parse(string? text, Type t)
    {
        Assert.True(CellValueParser.TryParse(text, Col(t), out var v, out var e), e);
        Assert.Null(e);
        return v;
    }

    private static string Fail(string text, Type t)
    {
        Assert.False(CellValueParser.TryParse(text, Col(t), out var v, out var e));
        Assert.Null(v);
        return e!;
    }

    [Fact]
    public void NullTextIsNullForEveryType()
    {
        Assert.Null(Parse(null, typeof(string)));
        Assert.Null(Parse(null, typeof(int)));
    }

    [Fact]
    public void EmptyTextIsEmptyStringOrNull()
    {
        Assert.Equal("", Parse("", typeof(string)));
        Assert.Null(Parse("  ", typeof(int)));
        Assert.Null(Parse("", typeof(Guid)));
        Assert.Null(Parse("", typeof(byte[])));
    }

    [Fact]
    public void BoolAcceptsWords()
    {
        Assert.Equal(true, Parse("YES", typeof(bool)));
        Assert.Equal(true, Parse("1", typeof(bool)));
        Assert.Equal(false, Parse("No", typeof(bool)));
        Assert.Equal(false, Parse("false", typeof(bool)));
        Assert.Equal("\"maybe\" is not a valid true/false value", Fail("maybe", typeof(bool)));
    }

    [Fact]
    public void IntegersParseAndCheckRange()
    {
        Assert.Equal((byte)255, Parse("255", typeof(byte)));
        Assert.Equal((short)-32768, Parse("-32768", typeof(short)));
        Assert.Equal(int.MaxValue, Parse(" 2147483647 ", typeof(int)));
        Assert.Equal(ulong.MaxValue, Parse("18446744073709551615", typeof(ulong)));
        Assert.Equal("\"256\" is out of range for byte", Fail("256", typeof(byte)));
        Assert.Equal("\"-1\" is out of range for uint", Fail("-1", typeof(uint)));
        Assert.Equal("\"1.5\" is not a valid integer value", Fail("1.5", typeof(int)));
        Assert.Equal("\"abc\" is not a valid integer value", Fail("abc", typeof(long)));
    }

    [Fact]
    public void FloatingPointUsesDotSeparator()
    {
        Assert.Equal(1.5, Parse("1.5", typeof(double)));
        Assert.Equal(1.5f, Parse("1.5", typeof(float)));
        Assert.Equal(1.25m, Parse("1.25e0", typeof(decimal)));
        Assert.Equal("\"1,5\" is not a valid number value", Fail("1,5", typeof(double)));
    }

    [Fact]
    public void DatesRequireIso()
    {
        Assert.Equal(new DateTime(2024, 1, 31), Parse("2024-01-31", typeof(DateTime)));
        Assert.Equal(new DateTime(2024, 1, 31, 10, 20, 30, 123), Parse("2024-01-31 10:20:30.123", typeof(DateTime)));
        Assert.Equal(new DateTime(2024, 1, 31, 10, 20, 30), Parse("2024-01-31T10:20:30", typeof(DateTime)));
        Assert.Equal(new DateTimeOffset(2024, 1, 31, 10, 20, 30, TimeSpan.FromHours(2)), Parse("2024-01-31 10:20:30+02:00", typeof(DateTimeOffset)));
        Assert.Equal(new DateTimeOffset(2024, 1, 31, 10, 20, 30, TimeSpan.Zero), Parse("2024-01-31T10:20:30Z", typeof(DateTimeOffset)));
        Assert.Equal("\"2024-01-31 10:20:30\" is not a valid date and time value", Fail("2024-01-31 10:20:30", typeof(DateTimeOffset)));
        Assert.Equal("\"31/01/2024\" is not a valid date and time value", Fail("31/01/2024", typeof(DateTime)));
        Assert.Equal("\"nope\" is not a valid date value", Fail("nope", typeof(DateOnly)));
        Assert.Equal("\"25:00\" is not a valid time value", Fail("25:00", typeof(TimeOnly)));
        Assert.Equal("\"x\" is not a valid time value", Fail("x", typeof(TimeSpan)));
    }

    [Fact]
    public void GuidAndBinary()
    {
        var g = Guid.NewGuid();
        Assert.Equal(g, Parse(g.ToString(), typeof(Guid)));
        Assert.Equal("\"zz\" is not a valid GUID value", Fail("zz", typeof(Guid)));
        Assert.Equal(new byte[] { 0xab, 0x01 }, (byte[])Parse("0xAB01", typeof(byte[]))!);
        Assert.Empty((byte[])Parse("0x", typeof(byte[]))!);
        Assert.Equal("\"0xabc\" is not a valid binary (0x...) value", Fail("0xabc", typeof(byte[])));
        Assert.Equal("\"ab\" is not a valid binary (0x...) value", Fail("ab", typeof(byte[])));
    }

    [Fact]
    public void TruncatedBinaryDisplayIsRejected()
    {
        var shown = CellFormatter.Format(new byte[40], Col(typeof(byte[])))!;
        Assert.EndsWith("…", shown);
        Fail(shown, typeof(byte[]));
    }

    [Fact]
    public void UnknownTypePassesTextThrough()
    {
        Assert.Equal("{\"a\":1}", Parse("{\"a\":1}", typeof(object)));
        Assert.Equal("", Parse("", typeof(object)));
    }

    public static TheoryData<object> RoundTripValues() => new()
    {
        "text", true, false, (byte)7, (short)-5, 42, -9L, (sbyte)-3, (ushort)9, 7u, ulong.MaxValue,
        1.5f, -2.25, 3.14159m, new DateTime(2024, 5, 6, 7, 8, 9), new DateTime(2024, 5, 6, 7, 8, 9, 120),
        new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.FromHours(-5)),
        new DateTimeOffset(2024, 5, 6, 7, 8, 9, 120, TimeSpan.FromMinutes(90)),
        new DateOnly(2024, 5, 6), new TimeOnly(7, 8), TimeSpan.FromSeconds(3725), Guid.NewGuid(),
        new byte[] { 1, 2, 255 }, Array.Empty<byte>(),
    };

    [Theory]
    [MemberData(nameof(RoundTripValues))]
    public void RoundTripsWithCellFormatter(object original)
    {
        var col = Col(original.GetType());
        var text = CellFormatter.Format(original, col);
        Assert.True(CellValueParser.TryParse(text, col, out var v, out var e), e);
        if (original is byte[] b)
        {
            Assert.Equal(b, (byte[])v!);
        }
        else
        {
            Assert.Equal(original, v);
        }
    }
}
