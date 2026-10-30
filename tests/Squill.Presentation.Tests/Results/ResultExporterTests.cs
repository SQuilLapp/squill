using System.Text;
using System.Text.Json;
using Squill.Core;
using Squill.Data;
using Squill.Presentation.Query;
using Squill.Presentation.Results;
using Xunit;

namespace Squill.Presentation.Tests.Results;

public sealed class ResultExporterTests
{
    private sealed class MemorySource(string[] names, object?[][] rows, bool complete = true) : IGridDataSource
    {
        public IReadOnlyList<ColumnInfo> Columns { get; } = [.. names.Select((n, i) => new ColumnInfo(n, "t", typeof(object), true, i))];
        public long RowCount => rows.Length;
        public bool IsComplete => complete;
        public bool IsTruncated => false;
        public object? GetCell(long row, int column) => rows[row][column];
        public event EventHandler? Changed { add { } remove { } }
    }

    private static async Task<byte[]> Export(IResultExporter exporter, IGridDataSource source)
    {
        using var ms = new MemoryStream();
        await exporter.ExportAsync(source, ms, TestContext.Current.CancellationToken);
        return ms.ToArray();
    }

    [Fact]
    public async Task Csv_QuotingNullsDatesAndBytes_FollowsRfc4180()
    {
        var source = new MemorySource(
            ["a", "b,c"],
            [
                ["x,y", "say \"hi\""],
                ["line1\r\nline2", null],
                [new DateTime(2026, 1, 2, 3, 4, 5), new byte[] { 0xab, 0xcd }],
                [true, 1.5m],
            ]);
        var bytes = await Export(new CsvResultExporter(), source);
        Assert.NotEqual(0xEF, bytes[0]);
        var expected = "a,\"b,c\"\r\n\"x,y\",\"say \"\"hi\"\"\"\r\n\"line1\r\nline2\",\r\n2026-01-02 03:04:05,0xabcd\r\ntrue,1.5\r\n";
        Assert.Equal(expected, Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task Json_TypesNullsAndDuplicateNames_AreWrittenNatively()
    {
        var source = new MemorySource(["id", "id", "name", "ok", "d", "n"], [[1, 2L, "a\"b", true, new DateTime(2026, 1, 2), null]]);
        var bytes = await Export(new JsonResultExporter(), source);
        using var doc = JsonDocument.Parse(bytes);
        var row = doc.RootElement[0];
        Assert.Equal(1, row.GetProperty("id").GetInt32());
        Assert.Equal(2, row.GetProperty("id_2").GetInt64());
        Assert.Equal("a\"b", row.GetProperty("name").GetString());
        Assert.True(row.GetProperty("ok").GetBoolean());
        Assert.Equal("2026-01-02 00:00:00", row.GetProperty("d").GetString());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("n").ValueKind);
    }

    [Fact]
    public async Task Export_HundredThousandRows_Completes()
    {
        var rows = new object?[100_000][];
        for (var i = 0; i < rows.Length; i++)
            rows[i] = [i, "row " + i];
        var source = new MemorySource(["i", "s"], rows);
        Assert.True((await Export(new CsvResultExporter(), source)).Length > 0);
        Assert.True((await Export(new JsonResultExporter(), source)).Length > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Export_IncompleteSource_Throws(bool json)
    {
        IResultExporter exporter = json ? new JsonResultExporter() : new CsvResultExporter();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await Export(exporter, new MemorySource(["a"], [], complete: false)));
    }

    private sealed class OneShotReader : IResultReader
    {
        private bool _done;
        public IReadOnlyList<ColumnInfo> Columns { get; } = [new ColumnInfo("c", "int", typeof(int), true, 0)];
        public long RecordsAffected => -1;

        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken)
        {
            if (_done)
                return ValueTask.FromResult<object?[][]?>(null);
            _done = true;
            return ValueTask.FromResult<object?[][]?>([[1], [2]]);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public bool CheckAccess() => true;
        public void Post(Action action) => action();
    }

    [Fact]
    public async Task ExportCommand_PickedPath_WritesFile()
    {
        var grid = new StreamingGridDataSource(new OneShotReader(), 100, new InlineDispatcher());
        await grid.StartAsync(TestContext.Current.CancellationToken);
        var dialogs = new FakeDialogService { SavePathResult = Path.Combine(Path.GetTempPath(), "squill-export-" + Guid.NewGuid().ToString("N") + ".csv") };
        var result = ResultViewModel.ForGrid("Result 1", grid, dialogs);
        try
        {
            await result.ExportCommand.ExecuteAsync(new CsvResultExporter());
            Assert.Equal("c\r\n1\r\n2\r\n", await File.ReadAllTextAsync(dialogs.SavePathResult, TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(dialogs.SavePathResult);
        }
    }

    [Fact]
    public async Task ExportCommand_UnwritablePath_ShowsError()
    {
        var grid = new StreamingGridDataSource(new OneShotReader(), 100, new InlineDispatcher());
        await grid.StartAsync(TestContext.Current.CancellationToken);
        var dialogs = new FakeDialogService { SavePathResult = Path.Combine(Path.GetTempPath(), "squill-missing-" + Guid.NewGuid().ToString("N"), "x.csv") };
        await ResultViewModel.ForGrid("Result 1", grid, dialogs).ExportCommand.ExecuteAsync(new CsvResultExporter());
        Assert.Single(dialogs.Errors);
    }
}
