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

    [Theory]
    [InlineData(1, "1 modification en attente")]
    [InlineData(3, "3 modifications en attente")]
    public void Pending_agrees_with_the_count(int count, string expected) =>
        Assert.Equal(expected, EditBarFormat.Pending(count));

    [Theory]
    [InlineData(1, 1, 1, "1 mise à jour · 1 insertion · 1 suppression")]
    [InlineData(2, 0, 0, "2 mises à jour")]
    [InlineData(0, 3, 1, "3 insertions · 1 suppression")]
    public void Changes_lists_only_the_non_zero_kinds(int updates, int inserts, int deletes, string expected) =>
        Assert.Equal(expected, EditBarFormat.Changes(updates, inserts, deletes));

    [Fact]
    public void Marker_names_the_key_of_an_editable_result()
    {
        var marker = EditBarFormat.Marker(["id", "lang"], null);
        Assert.Equal(("Modifiable · clé primaire id, lang", true), marker);
    }

    [Fact]
    public void Marker_gives_the_reason_of_a_read_only_result() =>
        Assert.Equal(("Lecture seule · no key", false), EditBarFormat.Marker(null, "no key"));

    [Theory]
    [InlineData(null)]
    [InlineData("loading")]
    public void Marker_is_absent_without_editor_or_reason(string? reason) =>
        Assert.Null(EditBarFormat.Marker(null, reason));

    [Fact]
    public void Footer_marks_a_truncated_result_with_a_plus() =>
        Assert.Equal("Lignes 1–5 sur 5+ chargées", EditBarFormat.Footer(5, true));
}
