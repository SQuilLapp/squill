using Squill.Core;
using Squill.Data;
using Squill.Presentation.Results;
using Xunit;

namespace Squill.Presentation.Tests.Results;

public sealed class GridDataSourceTests
{
    private static readonly ColumnInfo Col = new("c", "int", typeof(int), true, 0);

    private sealed class FakeReader(int total, CancellationTokenSource? cancelSource = null, int cancelAfterBatches = 0) : IResultReader
    {
        private int _next;
        private int _batches;
        public bool Disposed { get; private set; }
        public IReadOnlyList<ColumnInfo> Columns { get; } = [Col];
        public long RecordsAffected => -1;

        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_next >= total)
                return ValueTask.FromResult<object?[][]?>(null);
            var n = Math.Min(maxRows, total - _next);
            var rows = new object?[n][];
            for (var i = 0; i < n; i++)
                rows[i] = [_next + i];
            _next += n;
            if (++_batches == cancelAfterBatches)
                cancelSource?.Cancel();
            return ValueTask.FromResult<object?[][]?>(rows);
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task StartAsync_TenThousandRows_LoadsAllAndThrottlesChanged()
    {
        var reader = new FakeReader(10_000);
        var source = new StreamingGridDataSource(reader, 100_000, new ImmediateDispatcher());
        var changed = 0;
        source.Changed += (_, _) => changed++;

        await source.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal(10_000, source.RowCount);
        Assert.True(source.IsComplete);
        Assert.False(source.IsTruncated);
        Assert.Equal(0, source.GetCell(0, 0));
        Assert.Equal(4096, source.GetCell(4096, 0));
        Assert.Equal(9_999, source.GetCell(9_999, 0));
        Assert.Null(source.GetCell(10_000, 0));
        Assert.Null(source.GetCell(0, 5));
        Assert.InRange(changed, 1, 10_000 / 1000 + 2);
        Assert.True(reader.Disposed);
    }

    [Fact]
    public async Task StartAsync_MoreRowsThanMax_Truncates()
    {
        var source = new StreamingGridDataSource(new FakeReader(10_000), 2_500, new ImmediateDispatcher());

        await source.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2_500, source.RowCount);
        Assert.True(source.IsTruncated);
        Assert.True(source.IsComplete);
    }

    [Fact]
    public async Task StartAsync_ExactlyMaxRows_IsNotTruncated()
    {
        var source = new StreamingGridDataSource(new FakeReader(2_000), 2_000, new ImmediateDispatcher());

        await source.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2_000, source.RowCount);
        Assert.False(source.IsTruncated);
    }

    [Fact]
    public async Task StartAsync_CancelledMidStream_KeepsLoadedRowsAndStaysIncomplete()
    {
        using var cts = new CancellationTokenSource();
        var source = new StreamingGridDataSource(new FakeReader(10_000, cts, 3), 100_000, new ImmediateDispatcher());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.StartAsync(cts.Token));

        Assert.Equal(3_000, source.RowCount);
        Assert.False(source.IsComplete);
        Assert.Equal(2_999, source.GetCell(2_999, 0));
    }

    public static TheoryData<object?, string?> FormatCases => new()
    {
        { null, null },
        { true, "true" },
        { false, "false" },
        { 1234.5m, "1234.5" },
        { 1.5d, "1.5" },
        { "text", "text" },
        { new DateTime(2024, 3, 5, 14, 7, 9), "2024-03-05 14:07:09" },
        { new DateTime(2024, 3, 5, 14, 7, 9, 120), "2024-03-05 14:07:09.120" },
        { new DateTimeOffset(2024, 3, 5, 14, 7, 9, TimeSpan.FromHours(2)), "2024-03-05 14:07:09+02:00" },
        { new DateTimeOffset(2024, 3, 5, 14, 7, 9, 5, TimeSpan.FromHours(-5)), "2024-03-05 14:07:09.005-05:00" },
        { new byte[] { 0xDE, 0xAD }, "0xdead" },
        { new byte[33], "0x" + new string('0', 64) + "…" },
    };

    [Theory]
    [MemberData(nameof(FormatCases))]
    public void Format_Value_ReturnsExpectedText(object? value, string? expected) =>
        Assert.Equal(expected, CellFormatter.Format(value, Col));
}
