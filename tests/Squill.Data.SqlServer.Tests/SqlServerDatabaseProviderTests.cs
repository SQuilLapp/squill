using Microsoft.Data.SqlClient;
using Squill.Core;
using Xunit;

namespace Squill.Data.SqlServer.Tests;

public sealed class SqlServerDatabaseProviderTests(SqlServerFixture server) : IClassFixture<SqlServerFixture>
{
    // The container presents a self-signed certificate; the provider trusts it only for "localhost".
    private ValueTask<IDatabaseConnection> OpenAsync() =>
        new SqlServerDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "ms", DatabaseDialect.SqlServer, "localhost", server.Port, SqlServerFixture.Database, SqlServerFixture.UserName, null, null),
            SqlServerFixture.Password,
            CancellationToken.None);

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ExecuteAsync_TypedValuesAndNull_AreReadBack()
    {
        await using var connection = await OpenAsync();
        Assert.False(string.IsNullOrEmpty(connection.ServerVersion));

        await using var reader = await connection.ExecuteAsync("select 1 as i, cast(2.5 as float) as r, 'x' as s, null as n", [], CancellationToken.None);
        Assert.Equal(["i", "r", "s", "n"], reader.Columns.Select(c => c.Name));
        var row = Assert.Single(await reader.ReadAllAsync(10, CancellationToken.None));
        Assert.Equal(1, row[0]);
        Assert.Equal(2.5, row[1]);
        Assert.Equal("x", row[2]);
        Assert.Null(row[3]);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ExecuteAsync_Parameters_AreBoundByName()
    {
        await using var connection = await OpenAsync();
        await using var reader = await connection.ExecuteAsync("select @a, @b", [new QueryParameter("a", 7L), new QueryParameter("b", null)], CancellationToken.None);
        var row = Assert.Single(await reader.ReadAllAsync(10, CancellationToken.None));
        Assert.Equal(7L, row[0]);
        Assert.Null(row[1]);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task RecordsAffected_Insert_ReportsRowCount()
    {
        await using var connection = await OpenAsync();
        await using var reader = await connection.ExecuteAsync("declare @t table (i int); insert into @t values (1), (2), (3)", [], CancellationToken.None);
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
    public async Task ExecuteAsync_InvalidSql_ThrowsSqlException()
    {
        await using var connection = await OpenAsync();
        await Assert.ThrowsAsync<SqlException>(async () => await connection.ExecuteAsync("selec nope", [], CancellationToken.None));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ReadSchemaAsync_NotImplemented_ThrowsNotSupported()
    {
        await using var connection = await OpenAsync();
        await Assert.ThrowsAsync<NotSupportedException>(async () => await connection.ReadSchemaAsync(CancellationToken.None));
    }
}
