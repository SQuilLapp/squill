using MySqlConnector;
using Squill.Core;
using Xunit;

namespace Squill.Data.MySql.Tests;

public sealed class MySqlDatabaseProviderTests(MySqlFixture server) : IClassFixture<MySqlFixture>
{
    private ValueTask<IDatabaseConnection> OpenAsync() =>
        new MySqlDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "my", DatabaseDialect.MySql, server.Host, server.Port, MySqlFixture.Database, MySqlFixture.UserName, null, null),
            MySqlFixture.Password,
            CancellationToken.None);

    private static async Task<List<object?[]>> QueryAsync(IDatabaseConnection connection, string sql, params QueryParameter[] parameters)
    {
        await using var reader = await connection.ExecuteAsync(sql, parameters, CancellationToken.None);
        return await reader.ReadAllAsync(100, CancellationToken.None);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ExecuteAsync_TypedValuesAndNull_AreReadBack()
    {
        await using var connection = await OpenAsync();
        Assert.False(string.IsNullOrEmpty(connection.ServerVersion));

        await using var reader = await connection.ExecuteAsync("select 1 as i, 2.5e0 as r, 'x' as s, null as n", [], CancellationToken.None);
        Assert.Equal(["i", "r", "s", "n"], reader.Columns.Select(c => c.Name));
        var row = Assert.Single(await reader.ReadAllAsync(10, CancellationToken.None));
        Assert.Equal(1L, Convert.ToInt64(row[0], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(2.5, Convert.ToDouble(row[1], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("x", row[2]);
        Assert.Null(row[3]);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ExecuteAsync_Parameters_AreBoundByName()
    {
        await using var connection = await OpenAsync();
        var rows = await QueryAsync(connection, "select @a, @b", new QueryParameter("a", 7L), new QueryParameter("b", null));
        var row = Assert.Single(rows);
        Assert.Equal(7L, Convert.ToInt64(row[0], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Null(row[1]);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task RecordsAffected_Insert_ReportsRowCount()
    {
        await using var connection = await OpenAsync();
        await QueryAsync(connection, "create temporary table t (i int)");
        await using var reader = await connection.ExecuteAsync("insert into t values (1), (2), (3)", [], CancellationToken.None);
        Assert.Empty(reader.Columns);
        Assert.Equal(-1, reader.RecordsAffected);
        Assert.Null(await reader.ReadBatchAsync(10, CancellationToken.None));
        Assert.Equal(3, reader.RecordsAffected);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadBatchAsync_MaxRowsTwo_ReturnsBatchesOfTwo()
    {
        await using var connection = await OpenAsync();
        await using var reader = await connection.ExecuteAsync("select 1 union all select 2 union all select 3 union all select 4 union all select 5", [], CancellationToken.None);
        var sizes = new List<int>();
        while (await reader.ReadBatchAsync(2, CancellationToken.None) is { } batch)
            sizes.Add(batch.Length);
        Assert.Equal([2, 2, 1], sizes);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ExecuteAsync_InvalidSql_ThrowsMySqlException()
    {
        await using var connection = await OpenAsync();
        await Assert.ThrowsAsync<MySqlException>(async () => await connection.ExecuteAsync("selec nope", [], CancellationToken.None));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadSchemaAsync_NotImplemented_ThrowsNotSupported()
    {
        await using var connection = await OpenAsync();
        await Assert.ThrowsAsync<NotSupportedException>(async () => await connection.ReadSchemaAsync(CancellationToken.None));
    }
}
