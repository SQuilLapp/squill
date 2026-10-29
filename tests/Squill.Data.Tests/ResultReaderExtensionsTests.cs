using Xunit;
using Squill.Core;

namespace Squill.Data.Tests;

public sealed class ResultReaderExtensionsTests
{
    private static readonly ColumnInfo[] Columns = [new("id", "INTEGER", typeof(long), false, 0)];

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(10)]
    public async Task ReadAllAsync_AnyBatchSize_ReturnsAllRowsInOrder(int batchSize)
    {
        object?[][] rows = [[1L], [2L], [null], [4L], [5L]];
        var provider = new FakeDatabaseProvider(DatabaseDialect.Sqlite, Columns, rows);
        var profile = new ConnectionProfile(Guid.NewGuid(), "t", DatabaseDialect.Sqlite, null, null, null, null, ":memory:", null);

        await using var connection = await provider.OpenAsync(profile, null, TestContext.Current.CancellationToken);
        await using var stream = await connection.ExecuteAsync("select 1", [], TestContext.Current.CancellationToken);
        var all = await stream.ReadAllAsync(batchSize, TestContext.Current.CancellationToken);

        Assert.Equal(rows, all);
        Assert.Equal(5, stream.RecordsAffected);
    }

    [Fact]
    public async Task ReadBatchAsync_AfterEnd_ReturnsNull()
    {
        await using var stream = new FakeDatabaseProvider.FakeStream(Columns, []);
        Assert.Null(await stream.ReadBatchAsync(5, TestContext.Current.CancellationToken));
    }
}
