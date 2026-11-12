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
}
