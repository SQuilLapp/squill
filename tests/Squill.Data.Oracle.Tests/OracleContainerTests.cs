using Squill.Core;
using Xunit;

namespace Squill.Data.Oracle.Tests;

/// <summary>The proof of concept against a real Oracle Free server through the real bridge process; runs only with <c>SQUILL_DOCKER_TESTS=1</c>.</summary>
public sealed class OracleContainerTests(OracleFixture server) : IClassFixture<OracleFixture>
{
    private ValueTask<IDatabaseConnection> OpenAsync() =>
        new OracleDatabaseProvider().OpenAsync(
            new ConnectionProfile(Guid.NewGuid(), "ora", DatabaseDialect.Oracle, server.Host, server.Port, OracleFixture.Service, OracleFixture.UserName, null, null),
            OracleFixture.Password, CancellationToken.None);

    private static async Task<List<object?[]>> QueryAsync(IDatabaseConnection connection, string sql, params QueryParameter[] parameters)
    {
        await using var reader = await connection.ExecuteAsync(sql, parameters, CancellationToken.None);
        return await reader.ReadAllAsync(100, CancellationToken.None);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Query_TypedValuesAndNull_AreReadBack()
    {
        await using var connection = await OpenAsync();
        Assert.False(string.IsNullOrEmpty(connection.ServerVersion));

        await using var reader = await connection.ExecuteAsync(
            "select 1 as i, 2.5 as r, 'x' as s, null as n, date '2026-11-04' as d from dual", [], CancellationToken.None);
        Assert.Equal(["I", "R", "S", "N", "D"], reader.Columns.Select(c => c.Name));
        var row = Assert.Single(await reader.ReadAllAsync(10, CancellationToken.None));
        Assert.Equal([1m, 2.5m, "x", null, new DateTime(2026, 11, 4)], row);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Query_ParametersAndBatches_Work()
    {
        await using var connection = await OpenAsync();

        var rows = await QueryAsync(connection, "select :a + level from dual connect by level <= 250", new QueryParameter("a", 10L));
        Assert.Equal(250, rows.Count);
        Assert.Equal(11m, rows[0][0]);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Schema_CreatedTable_IsListed()
    {
        await using var connection = await OpenAsync();
        await QueryAsync(connection, "create table squill_poc (id number(10) primary key, name varchar2(40) not null)");
        try
        {
            await using (var insert = await connection.ExecuteAsync("insert into squill_poc values (1, 'a')", [], CancellationToken.None))
                Assert.Null(await insert.ReadBatchAsync(10, CancellationToken.None));

            var schema = await connection.ReadSchemaAsync(CancellationToken.None);
            var table = Assert.IsType<TableInfo>(schema.FindTable(OracleFixture.UserName.ToUpperInvariant(), "SQUILL_POC"));
            Assert.Equal(["ID", "NAME"], table.Columns.Select(c => c.Name));
            Assert.Equal(["NUMBER(10)", "VARCHAR2(40)"], table.Columns.Select(c => c.DataType));
            Assert.True(table.Columns[0].IsPrimaryKey);
            Assert.False(table.Columns[1].IsNullable);
            Assert.Contains(OracleFixture.UserName.ToUpperInvariant(), await connection.ListDatabasesAsync(CancellationToken.None));
        }
        finally
        {
            await QueryAsync(connection, "drop table squill_poc");
        }
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Query_SqlError_ThrowsOracleMessage()
    {
        await using var connection = await OpenAsync();
        var e = await Assert.ThrowsAsync<OracleBridgeException>(async () => await connection.ExecuteAsync("select * from no_such_table", [], CancellationToken.None));
        Assert.Contains("ORA-00942", e.Message);
    }
}
