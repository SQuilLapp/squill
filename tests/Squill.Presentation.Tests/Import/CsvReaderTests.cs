using System.Text;
using Squill.Presentation.Import;
using Xunit;

namespace Squill.Presentation.Tests.Import;

public sealed class CsvReaderTests
{
    private static readonly UTF8Encoding Utf8 = new(false);

    private static async Task<List<CsvRow>> ReadAsync(byte[] bytes, CsvOptions? options = null, CancellationToken ct = default)
    {
        using var stream = new MemoryStream(bytes);
        List<CsvRow> rows = [];
        await foreach (var row in CsvReader.ReadRowsAsync(stream, options ?? CsvOptions.Default, ct))
        {
            rows.Add(row);
        }

        return rows;
    }

    private static Task<List<CsvRow>> ReadAsync(string text, CsvOptions? options = null) => ReadAsync(Utf8.GetBytes(text), options, TestContext.Current.CancellationToken);

    [Fact]
    public void Sniffer_utf8_with_and_without_bom()
    {
        var plain = CsvSniffer.Detect(Utf8.GetBytes("a,b\né,2\n"));
        var bom = CsvSniffer.Detect([.. Encoding.UTF8.GetPreamble(), .. Utf8.GetBytes("a,b\n1,2\n")]);
        Assert.Equal(65001, plain.Encoding.CodePage);
        Assert.Equal(65001, bom.Encoding.CodePage);
    }

    [Fact]
    public async Task Bom_is_not_part_of_the_first_field()
    {
        var rows = await ReadAsync([.. Encoding.UTF8.GetPreamble(), .. Utf8.GetBytes("id,name\n1,x\n")], null, TestContext.Current.CancellationToken);
        Assert.Equal(["id", "name"], rows[0].Fields);
    }

    [Fact]
    public async Task Latin1_accents_are_detected_and_read()
    {
        byte[] bytes = [.. "nom;ville\n"u8, .. Encoding.Latin1.GetBytes("René;Genève\n")];
        var options = CsvSniffer.Detect(bytes);
        Assert.Equal(Encoding.Latin1.CodePage, options.Encoding.CodePage);
        var rows = await ReadAsync(bytes, options, TestContext.Current.CancellationToken);
        Assert.Equal(["René", "Genève"], rows[1].Fields);
    }

    [Fact]
    public void Utf16_bom_is_detected()
    {
        var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("a;b\n1;2\n")).ToArray();
        var options = CsvSniffer.Detect(bytes);
        Assert.Equal(1200, options.Encoding.CodePage);
        Assert.Equal(';', options.Delimiter);
    }

    [Theory]
    [InlineData(',')]
    [InlineData(';')]
    [InlineData('\t')]
    [InlineData('|')]
    public void Sniffer_finds_each_delimiter(char delimiter)
    {
        var text = string.Join('\n', Enumerable.Range(0, 10).Select(i => $"a{delimiter}b,c{delimiter}{i}"));
        Assert.Equal(delimiter, CsvSniffer.Detect(Utf8.GetBytes(text)).Delimiter);
    }

    [Fact]
    public void Sniffer_defaults_to_comma_for_one_column()
    {
        var options = CsvSniffer.Detect(Utf8.GetBytes("a\nb\n"));
        Assert.Equal(',', options.Delimiter);
        Assert.Equal('"', options.Quote);
    }

    [Fact]
    public async Task Quoted_delimiter_newline_and_doubled_quotes()
    {
        var rows = await ReadAsync("a,b\n\"x,1\",\"line1\r\nline2\"\n\"say \"\"hi\"\"\",z\n");
        Assert.Equal(["x,1", "line1\r\nline2"], rows[1].Fields);
        Assert.Equal(["say \"hi\"", "z"], rows[2].Fields);
        Assert.Equal(2, rows[1].LineNumber);
        Assert.Equal(4, rows[2].LineNumber);
        Assert.All(rows, r => Assert.False(r.IsRagged));
    }

    [Fact]
    public async Task Crlf_empty_fields_and_missing_final_newline()
    {
        var rows = await ReadAsync("a,b,c\r\n1,,3\r\n\r\n4,5,");
        Assert.Equal(3, rows.Count);
        Assert.Equal(["1", "", "3"], rows[1].Fields);
        Assert.Equal(["4", "5", ""], rows[2].Fields);
    }

    [Fact]
    public async Task Ragged_rows_are_flagged()
    {
        var rows = await ReadAsync("a,b\n1\n1,2,3\n1,2\n");
        Assert.Equal([false, true, true, false], rows.Select(r => r.IsRagged));
    }

    [Fact]
    public async Task Empty_file_has_no_rows()
    {
        Assert.Empty(await ReadAsync([], null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Cancellation_stops_the_enumeration()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ReadAsync("a,b\n1,2\n"u8.ToArray(), null, cts.Token));
    }

    [Fact]
    public async Task Preview_header_sample_and_count()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "id,name\n1,a\n2,b\n3,c\n", Utf8, TestContext.Current.CancellationToken);
            var preview = await CsvPreview.ReadAsync(path, CsvOptions.Default, sampleRows: 2, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(["id", "name"], preview.ColumnNames);
            Assert.Equal(2, preview.Sample.Count);
            Assert.Null(preview.TotalRows);
            Assert.Equal(new FileInfo(path).Length, preview.FileBytes);
            Assert.Equal(3, await CsvPreview.CountRowsAsync(path, CsvOptions.Default, TestContext.Current.CancellationToken));

            var noHeader = CsvOptions.Default with { HasHeader = false };
            var raw = await CsvPreview.ReadAsync(path, noHeader, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(["col1", "col2"], raw.ColumnNames);
            Assert.Equal(4, raw.Sample.Count);
            Assert.Equal(4, await CsvPreview.CountRowsAsync(path, noHeader, TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Preview_of_an_empty_file()
    {
        var path = Path.GetTempFileName();
        try
        {
            var preview = await CsvPreview.ReadAsync(path, CsvOptions.Default, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Empty(preview.ColumnNames);
            Assert.Empty(preview.Sample);
            Assert.Equal(0, await CsvPreview.CountRowsAsync(path, CsvOptions.Default, TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Two_hundred_megabytes_are_read_row_by_row()
    {
        var line = "12345,some text here,3.14159265\n"u8.ToArray();
        const string header = "id,name,value\n";
        using var source = new RepeatingSource(header, line, header.Length + (200L * 1024 * 1024 / line.Length * line.Length));
        long count = 0;
        await foreach (var row in CsvReader.ReadRowsAsync(source, CsvOptions.Default, TestContext.Current.CancellationToken))
        {
            count++;
            Assert.False(row.IsRagged);
        }

        Assert.True(count > 5_000_000);
    }

    /// <summary>A read-only stream that produces a header and then one line repeated, without storing the content.</summary>
    private sealed class RepeatingSource(string header, byte[] line, long length) : Stream
    {
        private readonly byte[] head = Encoding.UTF8.GetBytes(header);
        private long position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => length;

        public override long Position { get => position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = 0;
            while (n < count && position < length)
            {
                var index = position < head.Length ? (int)position : (int)((position - head.Length) % line.Length);
                buffer[offset + n++] = position < head.Length ? head[index] : line[index];
                position++;
            }

            return n;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
