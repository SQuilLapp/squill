using System.Text;
using System.Text.Json;
using Squill.Core;
using Squill.Data;
using Squill.Presentation.Query;
using Squill.Presentation.Results;
using Xunit;

namespace Squill.Presentation.Tests.Results;

public sealed class ResultExportBehaviorTests : IDisposable
{
    private readonly string _dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "squill-export-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose() => Directory.Delete(_dir, true);

    private sealed class Source(ColumnInfo[] columns, object?[] row, long rows = 1, Action<long>? onRow = null) : IGridDataSource
    {
        public IReadOnlyList<ColumnInfo> Columns => columns;
        public long RowCount => rows;
        public bool IsComplete => true;
        public bool IsTruncated => false;

        public object? GetCell(long r, int column)
        {
            if (column == 0)
                onRow?.Invoke(r);
            return row[column];
        }

        public event EventHandler? Changed { add { } remove { } }
    }

    private static ColumnInfo Col(string name, string type = "t") => new(name, type, typeof(object), true, 0);

    private static async Task<byte[]> ExportAsync(IResultExporter exporter, IGridDataSource source)
    {
        using var ms = new MemoryStream();
        await exporter.ExportAsync(source, ms, TestContext.Current.CancellationToken);
        return ms.ToArray();
    }

    [Fact]
    public async Task Csv_BinaryValue_IsWrittenInFullNotTruncated()
    {
        var bytes = Enumerable.Range(0, 100).Select(i => (byte)i).ToArray();
        var csv = Encoding.UTF8.GetString(await ExportAsync(new CsvResultExporter(), new Source([Col("b")], [bytes])));
        Assert.Equal("b\r\n0x" + Convert.ToHexStringLower(bytes) + "\r\n", csv);
    }

    [Fact]
    public async Task Json_ArraysJsonColumnsDatesAndNonFiniteNumbers_AreNativeOrIso()
    {
        ColumnInfo[] columns = [Col("arr"), Col("j", "jsonb"), Col("bad", "json"), Col("text"), Col("when"), Col("nan"), Col("inf"), Col("nested")];
        object?[] row = [new[] { 1, 2 }, "{\"a\": [1]}", "not json", "{\"a\":1}", new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.FromHours(2)), double.NaN, float.NegativeInfinity, new string?[] { "x", null }];
        using var doc = JsonDocument.Parse(await ExportAsync(new JsonResultExporter(), new Source(columns, row)));
        var r = doc.RootElement[0];
        Assert.Equal("[1,2]", r.GetProperty("arr").GetRawText().Replace(" ", "").Replace("\r", "").Replace("\n", ""));
        Assert.Equal(1, r.GetProperty("j").GetProperty("a")[0].GetInt32());
        Assert.Equal("not json", r.GetProperty("bad").GetString());
        Assert.Equal("{\"a\":1}", r.GetProperty("text").GetString());
        Assert.Equal("2024-05-06T07:08:09+02:00", r.GetProperty("when").GetString());
        Assert.Equal("NaN", r.GetProperty("nan").GetString());
        Assert.Equal("-Infinity", r.GetProperty("inf").GetString());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("nested")[1].ValueKind);
    }

    [Fact]
    public async Task Json_BinaryValue_IsBase64()
    {
        using var doc = JsonDocument.Parse(await ExportAsync(new JsonResultExporter(), new Source([Col("b")], [new byte[] { 0xca, 0xfe }])));
        Assert.Equal("yv4=", doc.RootElement[0].GetProperty("b").GetString());
    }

    private sealed class GeneratedReader(int rows) : IResultReader
    {
        private int _next;
        public IReadOnlyList<ColumnInfo> Columns { get; } = [new("id", "int", typeof(int), false, 0), new("name", "text", typeof(string), true, 1)];
        public long RecordsAffected => -1;

        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken)
        {
            if (_next >= rows)
                return ValueTask.FromResult<object?[][]?>(null);
            var batch = new object?[Math.Min(1000, rows - _next)][];
            for (var i = 0; i < batch.Length; i++, _next++)
                batch[i] = [_next, "row, \"" + _next + "\""];
            return ValueTask.FromResult<object?[][]?>(batch);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public bool CheckAccess() => true;
        public void Post(Action action) => action();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Export_HalfAMillionRows_StreamsToFileWithBoundedMemory(bool json)
    {
        var ct = TestContext.Current.CancellationToken;
        var grid = new StreamingGridDataSource(new GeneratedReader(500_000), 500_000, new InlineDispatcher());
        await grid.StartAsync(ct);
        Assert.Equal(500_000, grid.RowCount);
        IResultExporter exporter = json ? new JsonResultExporter() : new CsvResultExporter();
        var path = Path.Combine(_dir, "big." + exporter.FileExtension);

        var before = GC.GetTotalMemory(true);
        await ResultFileWriter.WriteAsync(exporter, grid, path, ct);
        var growth = GC.GetTotalMemory(true) - before;

        Assert.True(growth < 200L * 1024 * 1024, $"retained {growth} bytes");
        Assert.True(new FileInfo(path).Length > 10_000_000);
        Assert.Equal(path, Assert.Single(Directory.GetFiles(_dir)));
        if (!json)
        {
            var lines = 0;
            foreach (var _ in File.ReadLines(path))
                lines++;
            Assert.Equal(500_001, lines);
        }
        else
        {
            await using var stream = File.OpenRead(path);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            Assert.Equal(500_000, doc.RootElement.GetArrayLength());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancel_MidExport_LeavesNoPartialFileAndKeepsTheOldOne(bool existing)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var source = new Source([Col("a")], ["x"], 100_000, r =>
        {
            if (r == 50_000)
                cts.Cancel();
        });
        var path = Path.Combine(_dir, "out.csv");
        if (existing)
            await File.WriteAllTextAsync(path, "old", TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await ResultFileWriter.WriteAsync(new CsvResultExporter(), source, path, cts.Token));

        Assert.Equal(existing ? [path] : [], Directory.GetFiles(_dir));
        if (existing)
            Assert.Equal("old", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Failure_RemovesTheTemporaryFile()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ResultFileWriter.WriteAsync(new CsvResultExporter(), new IncompleteSource(), Path.Combine(_dir, "x.csv"), TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(_dir));
    }

    private sealed class IncompleteSource : IGridDataSource
    {
        public IReadOnlyList<ColumnInfo> Columns => [];
        public long RowCount => 0;
        public bool IsComplete => false;
        public bool IsTruncated => false;
        public object? GetCell(long row, int column) => null;
        public event EventHandler? Changed { add { } remove { } }
    }
}
