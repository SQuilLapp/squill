using Avalonia.Controls;
using Avalonia.Headless;
using Squill.App.Avalonia.Controls;
using Squill.Core;
using Squill.Presentation.Results;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class GridCellPreviewUiTests
{
    private static readonly string Json = "{\n" + string.Concat(Enumerable.Range(0, 3000).Select(i => $"    \"key{i}\": \"value {i}\",\n")) + "    \"end\": 1\n}";

    private sealed class Source : IGridDataSource
    {
        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public IReadOnlyList<ColumnInfo> Columns { get; } = [new("doc", "jsonb", typeof(string), true, 0)];
        public long RowCount => 3;
        public bool IsComplete => true;
        public bool IsTruncated => false;
        public object? GetCell(long row, int column) => Json;
    }

    private static string Canvas
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(dir.FullName, "Squill.slnx")))
            {
                dir = dir.Parent!;
            }

            return File.ReadAllText(Path.Combine(dir.FullName, "src", "Squill.App.Avalonia", "Controls", "DataGridCanvas.cs"));
        }
    }

    [Fact]
    public Task A_100_KB_multi_line_cell_renders_without_throwing() => Headless.Session.Dispatch(() =>
    {
        Assert.True(Json.Length > 50_000);
        var view = new DataGridView { DataSource = new Source() };
        var window = new Window { Width = 600, Height = 300, Content = view };
        window.Show();
        view.Canvas.InvalidateVisual();
        window.CaptureRenderedFrame();
        window.Close();
    }, TestContext.Current.CancellationToken);

    [Fact]
    public void Draw_and_measure_use_the_single_line_preview() =>
        Assert.True(Canvas.Split("CellText.Preview(").Length - 1 >= 2);

    [Fact]
    public void Overflowing_cells_trim_with_an_ellipsis_and_never_wrap()
    {
        var code = Canvas;
        Assert.Contains("MaxLineCount = 1", code);
        Assert.Contains("TextTrimming.CharacterEllipsis", code);
    }
}
