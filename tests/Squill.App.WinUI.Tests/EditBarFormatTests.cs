using Squill.App.WinUI.Views;
using Squill.Core.Editing;
using Xunit;

namespace Squill.App.WinUI.Tests;

public class EditBarFormatTests
{
    [Fact]
    public void Describe_numbers_rows_from_one_and_names_the_column() =>
        Assert.Equal("Row 3, name: Required", EditBarFormat.Describe(new EditIssue(2, "name", "Required")));

    [Fact]
    public void Describe_omits_the_column_of_a_row_level_issue() =>
        Assert.Equal("Row 1: Failed", EditBarFormat.Describe(new EditIssue(0, null, "Failed")));

    [Theory]
    [InlineData("b", 1)]
    [InlineData("zzz", 0)]
    [InlineData(null, 0)]
    public void ColumnIndex_falls_back_to_the_first_column(string? column, int expected) =>
        Assert.Equal(expected, EditBarFormat.ColumnIndex(new EditIssue(0, column, "x"), ["a", "b"]));
}
