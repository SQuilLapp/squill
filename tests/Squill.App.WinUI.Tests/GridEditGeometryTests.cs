using Squill.App.WinUI.Controls;
using Squill.Presentation.Results;
using Xunit;

namespace Squill.App.WinUI.Tests;

public class GridEditGeometryTests
{
    private static GridLayout Layout()
    {
        var layout = new GridLayout(26, 30);
        layout.SetColumnWidths([100, 150, 80]);
        return layout;
    }

    [Fact]
    public void Editor_box_sits_over_the_cell_and_follows_the_scroll_offsets()
    {
        var box = GridEditGeometry.EditorBox(Layout(), 2, 1, 20, 10, 400, 200);

        Assert.Equal(new CellBox(80, 30 + 52 - 10, 150, 26), box);
    }

    [Theory]
    [InlineData(100, 0, 0)]   // row 100 is far below the body
    [InlineData(0, 40, 30)]   // the row scrolled under the header
    [InlineData(0, 0, 500)]   // the column scrolled out on the left
    public void Editor_box_is_null_when_the_cell_is_scrolled_out(long row, double scrollY, double scrollX)
    {
        Assert.Null(GridEditGeometry.EditorBox(Layout(), row, 1, scrollX, scrollY, 400, 200));
    }

    [Fact]
    public void Editor_box_is_null_for_an_unknown_column()
    {
        Assert.Null(GridEditGeometry.EditorBox(Layout(), 0, 3, 0, 0, 400, 200));
        Assert.Null(GridEditGeometry.EditorBox(Layout(), 0, -1, 0, 0, 400, 200));
    }

    [Theory]
    [InlineData(RowState.Unchanged, GridTint.None)]
    [InlineData(RowState.Modified, GridTint.Modified)]
    [InlineData(RowState.Inserted, GridTint.Inserted)]
    [InlineData(RowState.Deleted, GridTint.Deleted)]
    public void Row_state_selects_the_tint(RowState state, GridTint expected) =>
        Assert.Equal(expected, GridEditGeometry.RowTint(state));

    [Theory]
    [InlineData(RowState.Modified, true, true)]
    [InlineData(RowState.Modified, false, false)]
    [InlineData(RowState.Inserted, true, false)]
    public void Only_modified_cells_of_existing_rows_get_the_extra_tint(RowState state, bool modified, bool expected) =>
        Assert.Equal(expected, GridEditGeometry.ModifiedCellTint(state, modified));
}
