using Npgsql;
using Squill.Core;
using Xunit;

namespace Squill.Data.Postgres.Tests;

public sealed class PostgresDatabaseProviderTests(PostgresFixture server) : IClassFixture<PostgresFixture>
{
    private ValueTask<IDatabaseConnection> OpenAsync() =>
        new PostgresDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "pg", DatabaseDialect.PostgreSql, server.Host, server.Port, PostgresFixture.Database, PostgresFixture.UserName, null, null),
            PostgresFixture.Password,
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

        await using var reader = await connection.ExecuteAsync("select 1 as i, 2.5::float8 as r, 'x' as s, null::text as n", [], CancellationToken.None);
        Assert.Equal(["i", "r", "s", "n"], reader.Columns.Select(c => c.Name));
        Assert.Equal("integer", reader.Columns[0].DataTypeName);
        Assert.Equal(typeof(int), reader.Columns[0].ClrType);
        var row = Assert.Single(await reader.ReadAllAsync(10, CancellationToken.None));
        Assert.Equal([1, 2.5, "x", null], row);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ExecuteAsync_Parameters_AreBoundByName()
    {
        await using var connection = await OpenAsync();
        var rows = await QueryAsync(connection, "select @a::int8, @b::text", new QueryParameter("a", 7L), new QueryParameter("b", null));
        Assert.Equal([7L, null], Assert.Single(rows));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task RecordsAffected_Insert_ReportsRowCount()
    {
        await using var connection = await OpenAsync();
        await QueryAsync(connection, "create temp table t (i int)");
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
        await using var reader = await connection.ExecuteAsync("select generate_series(1, 5)", [], CancellationToken.None);
        var sizes = new List<int>();
        while (await reader.ReadBatchAsync(2, CancellationToken.None) is { } batch)
            sizes.Add(batch.Length);
        Assert.Equal([2, 2, 1], sizes);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ExecuteAsync_InvalidSql_ThrowsPostgresException()
    {
        await using var connection = await OpenAsync();
        await Assert.ThrowsAsync<PostgresException>(async () => await connection.ExecuteAsync("selec nope", [], CancellationToken.None));
    }
}
