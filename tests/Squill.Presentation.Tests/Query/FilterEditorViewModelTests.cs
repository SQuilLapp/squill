using Squill.Presentation.Localization;
using Squill.Presentation.Query;
using Squill.Presentation.Results;
using Xunit;

namespace Squill.Presentation.Tests.Query;

public sealed class FilterEditorViewModelTests : ViewStateFixture
{
    private async Task<(ResultViewModel Result, FilterEditorViewModel Editor)> OpenAsync()
    {
        var result = await RunAsync("SELECT * FROM t");
        return (result, result.View!.OpenFilterEditor()!);
    }

    [Fact]
    public async Task Operators_FollowTheColumnCategory_WithoutIn()
    {
        var (_, editor) = await OpenAsync();

        editor.SelectedColumn = editor.Columns[1];
        var text = editor.Operators.Select(o => o.Operator).ToList();
        editor.SelectedColumn = editor.Columns[2];
        var number = editor.Operators.Select(o => o.Operator).ToList();

        Assert.Contains(GridFilterOperator.Contains, text);
        Assert.DoesNotContain(GridFilterOperator.GreaterThan, text);
        Assert.Contains(GridFilterOperator.GreaterThan, number);
        Assert.DoesNotContain(GridFilterOperator.In, text.Concat(number));
    }

    [Fact]
    public async Task SwitchingColumn_ResetsTheOperator()
    {
        var (_, editor) = await OpenAsync();
        editor.SelectedColumn = editor.Columns[2];
        editor.SelectedOperator = editor.Operators[^1];

        editor.SelectedColumn = editor.Columns[1];

        Assert.Same(editor.Operators[0], editor.SelectedOperator);
    }

    [Theory]
    [InlineData(UiLanguage.French, "est égal à")]
    [InlineData(UiLanguage.English, "equals")]
    public async Task OperatorNames_AreLocalized(UiLanguage language, string expected)
    {
        using var _ = UiText.Override(language);
        var (_, editor) = await OpenAsync();

        Assert.Equal(expected, editor.Operators.Single(o => o.Operator == GridFilterOperator.Equals).Text);
    }

    [Fact]
    public async Task Apply_AddsTheFilterAndCloses()
    {
        var (result, editor) = await OpenAsync();
        editor.SelectedColumn = editor.Columns[2];
        editor.SelectedOperator = editor.Operators.Single(o => o.Operator == GridFilterOperator.GreaterThan);
        editor.OperandText = "15";

        editor.ApplyCommand.Execute(null);

        Assert.Null(editor.Error);
        Assert.False(editor.IsOpen);
        Assert.Equal(GridFilterOperator.GreaterThan, Assert.Single(result.View!.Filters).Filter.Operator);
    }

    [Fact]
    public async Task Apply_BadNumber_SetsErrorAndKeepsThePopupOpen()
    {
        var (result, editor) = await OpenAsync();
        editor.SelectedColumn = editor.Columns[2];
        editor.SelectedOperator = editor.Operators.Single(o => o.Operator == GridFilterOperator.GreaterThan);
        editor.OperandText = "abc";

        editor.ApplyCommand.Execute(null);

        Assert.NotNull(editor.Error);
        Assert.True(editor.IsOpen);
        Assert.Empty(result.View!.Filters);
    }

    [Fact]
    public async Task IsNull_NeedsNoOperand()
    {
        var (result, editor) = await OpenAsync();
        editor.SelectedColumn = editor.Columns[1];
        editor.SelectedOperator = editor.Operators.Single(o => o.Operator == GridFilterOperator.IsNull);
        Assert.False(editor.NeedsOperand);

        editor.ApplyCommand.Execute(null);

        Assert.Single(result.View!.Filters);
        editor.SelectedOperator = editor.Operators[0];
        Assert.True(editor.NeedsOperand);
    }

    [Fact]
    public async Task Cancel_Closes_AndPendingChangesBlockApply()
    {
        var (result, editor) = await OpenAsync();
        editor.CancelCommand.Execute(null);
        Assert.False(editor.IsOpen);

        result.Editor!.TrySetCell(0, 2, "11");
        Assert.False(editor.CanChange);
        Assert.Equal(UiText.Get("results.filter.blocked"), editor.BlockedReason);
        Assert.False(editor.ApplyCommand.CanExecute(null));
    }
}
