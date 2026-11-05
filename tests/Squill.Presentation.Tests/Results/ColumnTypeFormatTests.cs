using System.Text.Json;
using Squill.Core;
using Squill.Presentation.Results;
using Xunit;

namespace Squill.Presentation.Tests.Results;

public sealed class ColumnTypeFormatTests
{
    private static readonly int[] IntArray = [1, 2, 3];
    private static readonly string[] TextArray = ["a", "b c"];

    private static ColumnInfo Col(string type = "t") => new("c", type, typeof(object), true, 0);

    [Fact]
    public void ArraysUseThePostgresLiteralStyle()
    {
        Assert.Equal("{1,2,3}", CellFormatter.Format(IntArray, Col()));
        Assert.Equal("{a,b c}", CellFormatter.Format(TextArray, Col()));
        Assert.Equal("{}", CellFormatter.Format(Array.Empty<int>(), Col()));
        Assert.Equal("{1,x}", CellFormatter.Format(new object[] { 1, "x" }, Col()));
    }

    [Fact]
    public void JsonIsShownAsText()
    {
        using var document = JsonDocument.Parse("""{"a":1}""");
        Assert.Equal("""{"a":1}""", CellFormatter.Format(document, Col()));
        Assert.Equal("""{"a":1}""", CellFormatter.Format(document.RootElement, Col()));
    }

    [Fact]
    public void GuidIsLowerCaseWithDashes() =>
        Assert.Equal("a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11", CellFormatter.Format(Guid.Parse("A0EEBC99-9C0B-4EF8-BB6D-6BB9BD380A11"), Col()));

    [Theory]
    [InlineData(0, 2, 3, 4, 0, "02:03:04")]
    [InlineData(1, 2, 3, 4, 0, "1.02:03:04")]
    [InlineData(0, 2, 3, 4, 500, "02:03:04.5")]
    [InlineData(-1, -2, -3, -4, -500, "-1.02:03:04.5")]
    [InlineData(0, -12, -3, -4, 0, "-12:03:04")]
    public void TimeSpanIsSignDaysHoursMinutesSeconds(int days, int hours, int minutes, int seconds, int milliseconds, string expected) =>
        Assert.Equal(expected, CellFormatter.Format(new TimeSpan(days, hours, minutes, seconds, milliseconds), Col()));

    [Fact]
    public void DateAndTimeOnlyAreInvariantIso()
    {
        Assert.Equal("2024-05-06", CellFormatter.Format(new DateOnly(2024, 5, 6), Col()));
        Assert.Equal("07:08:09", CellFormatter.Format(new TimeOnly(7, 8, 9), Col()));
        Assert.Equal("07:08:09.123", CellFormatter.Format(new TimeOnly(7, 8, 9, 123), Col()));
    }

    [Fact]
    public void DateColumnsShowNoTimeOfDay()
    {
        Assert.Equal("2024-05-06", CellFormatter.Format(new DateTime(2024, 5, 6), Col("DATE")));
        Assert.Equal("2024-05-06 00:00:00", CellFormatter.Format(new DateTime(2024, 5, 6), Col("DATETIME")));
    }

    [Fact]
    public void MySqlZeroDatesReadAsMinValueAndShowAsZeros()
    {
        Assert.Equal("0000-00-00", CellFormatter.Format(DateTime.MinValue, Col("DATE")));
        Assert.Equal("0000-00-00 00:00:00", CellFormatter.Format(DateTime.MinValue, Col("DATETIME")));
    }

    [Fact]
    public void SpecialFloatsAndDecimalScale()
    {
        Assert.Equal("NaN", CellFormatter.Format(double.NaN, Col()));
        Assert.Equal("Infinity", CellFormatter.Format(double.PositiveInfinity, Col()));
        Assert.Equal("-Infinity", CellFormatter.FormatDisplay(double.NegativeInfinity, Col()));
        Assert.Equal("1234.5000", CellFormatter.Format(1234.5000m, Col()));
    }

    [Fact]
    public void BinaryIsCutAtTheHexLimit()
    {
        var text = CellFormatter.Format(new byte[1 << 20], Col())!;
        Assert.Equal(2 + 64 + 1, text.Length);
        Assert.EndsWith("…", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AValueThatCannotBeFormattedNeverThrows() =>
        Assert.Equal("<unsupported: Broken>", CellFormatter.Format(new Broken(), Col()));

    private sealed class Broken : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider) => throw new InvalidOperationException();
    }
}
