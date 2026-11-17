using Squill.Presentation.Localization;
using Squill.Presentation.Query;
using Squill.Presentation.Results;
using Xunit;

namespace Squill.Presentation.Tests.Query;

public sealed class ColumnMenuViewModelTests : ViewStateFixture
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    private async Task<(ResultViewModel Result, ColumnMenuViewModel Menu)> OpenAsync(int column)
    {
        var result = await RunAsync("SELECT * FROM t");
        var menu = result.View!.OpenColumnMenu(column)!;
        await menu.ValuesLoaded.WaitAsync(Bound, TestContext.Current.CancellationToken);
        return (result, menu);
    }

    private static async Task<string> QtyAsync(ResultViewModel result)
    {
        if (result.DisplayGrid is FilteredGridDataSource view)
            await view.WhenIdle().WaitAsync(Bound, TestContext.Current.CancellationToken);
        var grid = result.DisplayGrid!;
        return string.Join(",", Enumerable.Range(0, (int)grid.RowCount).Select(i => grid.GetCell(i, 2)));
    }

    [Fact]
    public async Task Sort_AscendingThenDescendingOnTheSameColumn()
    {
        var (result, menu) = await OpenAsync(2);
        Assert.Null(menu.CurrentSort);

        menu.SortAscendingCommand.Execute(null);
        Assert.Equal(new GridSort(2, false), menu.CurrentSort);
        Assert.Equal("10,20,30,40", await QtyAsync(result));

        menu.SortDescendingCommand.Execute(null);
        Assert.Equal(new GridSort(2, true), menu.CurrentSort);
        Assert.Equal("40,30,20,10", await QtyAsync(result));
    }

    [Fact]
    public async Task Values_CountsWithNullRowAndSearchNarrows()
    {
        var (_, menu) = await OpenAsync(1);

        Assert.False(menu.IsLoadingValues);
        Assert.Equal(["a", "NULL", "b"], menu.Values.Select(v => v.Text));
        Assert.Equal([2, 1, 1], menu.Values.Select(v => v.Count));
        Assert.All(menu.Values, v => Assert.True(v.IsChecked));
        Assert.Null(menu.Values[1].Value);
        Assert.False(menu.ValuesTruncated);
        Assert.Null(menu.ValuesHint);

        menu.ValueSearch = "B";
        Assert.Equal(["b"], menu.Values.Select(v => v.Text));
        menu.ValueSearch = "";
        Assert.Equal(3, menu.Values.Count);
    }

    [Fact]
    public async Task Search_UnchecksEveryValueItHidesAndApplyKeepsOnlyTheMatches()
    {
        var (result, menu) = await OpenAsync(1);

        menu.ValueSearch = "b";
        Assert.True(menu.Values.Single().IsChecked);
        menu.ValueSearch = "";
        Assert.Equal([false, false, true], menu.Values.Select(v => v.IsChecked));

        menu.ApplyValuesCommand.Execute(null);

        Assert.Equal("20", await QtyAsync(result));
    }

    [Fact]
    public async Task ApplyValues_BuildsAnInFilterWithNullAndReplacesIt()
    {
        var (result, menu) = await OpenAsync(1);
        menu.Values.Single(v => v.Text == "b").IsChecked = false;

        menu.ApplyValuesCommand.Execute(null);

        var filter = Assert.Single(result.View!.Filters).Filter;
        Assert.Equal(GridFilterOperator.In, filter.Operator);
        Assert.Equal(2, filter.Values!.Count);
        Assert.Contains(null, filter.Values);
        Assert.Equal("10,30,40", await QtyAsync(result));

        menu.Values.Single(v => v.Text == "NULL").IsChecked = false;
        menu.ApplyValuesCommand.Execute(null);
        Assert.Single(result.View.Filters);
        Assert.Equal("10,30", await QtyAsync(result));
    }

    [Fact]
    public async Task ApplyValues_AllChecked_RemovesTheColumnFilter()
    {
        var (result, menu) = await OpenAsync(1);
        menu.Values[0].IsChecked = false;
        menu.ApplyValuesCommand.Execute(null);
        Assert.Single(result.View!.Filters);

        menu.Values[0].IsChecked = true;
        menu.ApplyValuesCommand.Execute(null);

        Assert.Empty(result.View.Filters);
    }

    [Fact]
    public async Task ExistingFilter_PreChecksItsValues_AndClearColumnFilterRemovesIt()
    {
        var (result, first) = await OpenAsync(1);
        first.Values.Single(v => v.Text == "a").IsChecked = false;
        first.ApplyValuesCommand.Execute(null);

        var menu = result.View!.OpenColumnMenu(1)!;
        await menu.ValuesLoaded.WaitAsync(Bound, TestContext.Current.CancellationToken);

        Assert.Equal(["a", "NULL", "b"], menu.Values.Select(v => v.Text));
        Assert.Equal([false, true, true], menu.Values.Select(v => v.IsChecked));

        menu.ClearColumnFilterCommand.Execute(null);
        Assert.Empty(result.View.Filters);
        Assert.All(menu.Values, v => Assert.True(v.IsChecked));
    }

    [Fact]
    public async Task PendingChanges_BlockTheMenu()
    {
        var (result, menu) = await OpenAsync(1);
        result.Editor!.TrySetCell(0, 2, "11");
        Assert.False(menu.CanSort);
        Assert.Equal(UiText.Get("results.filter.blocked"), menu.BlockedReason);

        menu.SortAscendingCommand.Execute(null);
        menu.Values[0].IsChecked = false;
        menu.ApplyValuesCommand.Execute(null);

        Assert.False(result.View!.IsActive);
    }

    [Fact]
    public async Task ColumnName_AndOutOfRangeColumn()
    {
        var (result, menu) = await OpenAsync(2);
        Assert.Equal("qty", menu.ColumnName);
        Assert.Null(result.View!.OpenColumnMenu(7));
    }
}
