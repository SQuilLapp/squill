using Squill.Core;
using Squill.Presentation.Editing;
using Xunit;

namespace Squill.Presentation.Tests.Editing;

public sealed class CellEditorKindTests
{
    [Theory]
    [InlineData(typeof(bool), "bool", CellEditorKind.Boolean)]
    [InlineData(typeof(int), "int4", CellEditorKind.Whole)]
    [InlineData(typeof(long), "bigint", CellEditorKind.Whole)]
    [InlineData(typeof(decimal), "numeric", CellEditorKind.Fractional)]
    [InlineData(typeof(double), "float8", CellEditorKind.Fractional)]
    [InlineData(typeof(DateOnly), "date", CellEditorKind.Date)]
    [InlineData(typeof(TimeOnly), "time", CellEditorKind.Time)]
    [InlineData(typeof(DateTime), "timestamp", CellEditorKind.DateTime)]
    [InlineData(typeof(string), "jsonb", CellEditorKind.Json)]
    [InlineData(typeof(string), "JSON", CellEditorKind.Json)]
    [InlineData(typeof(string), "text", CellEditorKind.Text)]
    [InlineData(typeof(Guid), "uuid", CellEditorKind.Text)]
    public void For_ChoosesTheEditorFromTheColumnType(Type clr, string dataType, CellEditorKind expected) =>
        Assert.Equal(expected, CellEditorKinds.For(new ColumnInfo("c", dataType, clr, true, 0)));

    [Theory]
    [InlineData(CellEditorKind.Whole, "12a-3.4", "12-34")]
    [InlineData(CellEditorKind.Fractional, "1,2.5e3x", "12.5e3")]
    [InlineData(CellEditorKind.Text, "12a", "12a")]
    [InlineData(CellEditorKind.Date, "2020-01-01", "2020-01-01")]
    public void Sanitize_DropsCharactersTheTypeNeverTakes(CellEditorKind kind, string text, string expected) =>
        Assert.Equal(expected, CellEditorKinds.Sanitize(kind, text));

    [Theory]
    [InlineData(CellEditorKind.Date, 2026, 2, 31, 7, 8, 9, "2026-02-28")]
    [InlineData(CellEditorKind.DateTime, 2026, 11, 13, 7, 44, 16, "2026-11-13 07:44:16")]
    public void FormatMoment_BuildsTheTextAndClampsTheDay(CellEditorKind kind, int y, int mo, int d, int h, int mi, int s, string expected) =>
        Assert.Equal(expected, CellEditorKinds.FormatMoment(kind, y, mo, d, h, mi, s));

    [Fact]
    public void HasPicker_IsTrueForBooleansAndDates() =>
        Assert.Equal([true, true, true, false, false], new[] { CellEditorKind.Boolean, CellEditorKind.Date, CellEditorKind.DateTime, CellEditorKind.Text, CellEditorKind.Whole }.Select(CellEditorKinds.HasPicker));
}
