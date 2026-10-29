using Xunit;
using Microsoft.Data.Sqlite;
using Squill.Core;

namespace Squill.Data.Sqlite.Tests;

public sealed class SqliteDatabaseProviderTests
{
    private static readonly ConnectionProfile Memory =
        new(Guid.NewGuid(), "mem", DatabaseDialect.Sqlite, null, null, null, null, ":memory:", null);

    private static ValueTask<IDatabaseConnection> OpenAsync(ConnectionProfile? profile = null) =>
        new SqliteDatabaseProvider().OpenAsync(profile ?? Memory, null, CancellationToken.None);

    private static async Task<List<object?[]>> QueryAsync(IDatabaseConnection connection, string sql, params QueryParameter[] parameters)
    {
        await using var reader = await connection.ExecuteAsync(sql, parameters, CancellationToken.None);
        return await reader.ReadAllAsync(100, CancellationToken.None);
    }

    [Fact]
    public void Dialect_Provider_IsSqlite() =>
        Assert.Equal(DatabaseDialect.Sqlite, new SqliteDatabaseProvider().Dialect);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task OpenAsync_MissingFilePath_ThrowsArgumentException(string? path) =>
        await Assert.ThrowsAsync<ArgumentException>(async () => await OpenAsync(Memory with { FilePath = path }));

    [Fact]
    public async Task ExecuteAsync_TypedValuesAndNull_AreReadBack()
    {
        await using var connection = await OpenAsync();
        Assert.False(string.IsNullOrEmpty(connection.ServerVersion));
        await QueryAsync(connection, "CREATE TABLE t (i INTEGER, r REAL, s TEXT, b BLOB, n TEXT)");
        await QueryAsync(connection, "INSERT INTO t VALUES (1, 2.5, 'x', X'0102', NULL)");

        await using var reader = await connection.ExecuteAsync("SELECT i, r, s, b, n FROM t", [], CancellationToken.None);
        Assert.Equal(["i", "r", "s", "b", "n"], reader.Columns.Select(c => c.Name));
        Assert.Equal("INTEGER", reader.Columns[0].DataTypeName);
        Assert.Equal(typeof(long), reader.Columns[0].ClrType);
        Assert.All(reader.Columns, c => Assert.True(c.IsNullable));
        var row = Assert.Single(await reader.ReadAllAsync(10, CancellationToken.None));
        Assert.Equal([1L, 2.5, "x", new byte[] { 1, 2 }, null], row);
    }

    [Fact]
    public async Task ExecuteAsync_Parameters_AreBoundByName()
    {
        await using var connection = await OpenAsync();
        var rows = await QueryAsync(connection, "SELECT @a, @b", new QueryParameter("a", 7L), new QueryParameter("b", null));
        Assert.Equal([7L, null], Assert.Single(rows));
    }

    [Fact]
    public async Task RecordsAffected_Insert_ReportsRowCount()
    {
        await using var connection = await OpenAsync();
        await QueryAsync(connection, "CREATE TABLE t (i INTEGER)");
        await using var reader = await connection.ExecuteAsync("INSERT INTO t VALUES (1), (2), (3)", [], CancellationToken.None);
        Assert.Empty(reader.Columns);
        Assert.Equal(-1, reader.RecordsAffected);
        Assert.Null(await reader.ReadBatchAsync(10, CancellationToken.None));
        Assert.Equal(3, reader.RecordsAffected);
    }

    [Fact]
    public async Task ReadBatchAsync_MaxRowsTwo_ReturnsBatchesOfTwo()
    {
        await using var connection = await OpenAsync();
        await using var reader = await connection.ExecuteAsync("SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4 UNION ALL SELECT 5", [], CancellationToken.None);
        var sizes = new List<int>();
        while (await reader.ReadBatchAsync(2, CancellationToken.None) is { } batch)
            sizes.Add(batch.Length);
        Assert.Equal([2, 2, 1], sizes);
    }

    [Fact]
    public async Task ReadBatchAsync_CancelledToken_Throws()
    {
        await using var connection = await OpenAsync();
        await using var reader = await connection.ExecuteAsync("SELECT 1 UNION ALL SELECT 2", [], CancellationToken.None);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await reader.ReadBatchAsync(10, cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_InvalidSql_ThrowsSqliteException()
    {
        await using var connection = await OpenAsync();
        await Assert.ThrowsAsync<SqliteException>(async () => await connection.ExecuteAsync("SELEC nope", [], CancellationToken.None));
    }
}
