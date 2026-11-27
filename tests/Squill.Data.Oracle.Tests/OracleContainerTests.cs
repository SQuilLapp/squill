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

    private static async Task RunAsync(IDatabaseConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var reader = await connection.ExecuteAsync(sql, [], cancellationToken);
        while (await reader.NextResultAsync(cancellationToken))
        {
        }
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Cancel_RunningStatement_IsInterruptedAndTheConnectionStaysUsable()
    {
        await using var connection = await OpenAsync();

        // Not DBMS_SESSION.SLEEP(30): Oracle Free 23ai does not service a break while the session sleeps, so a statement that works is cancelled instead.
        var started = System.Diagnostics.Stopwatch.StartNew();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await RunAsync(connection, "select count(*) from (select level l from dual connect by level <= 100000) a, (select level m from dual connect by level <= 100000) b where mod(a.l * b.m, 7) = 3", cts.Token));
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(15), $"cancel took {started.Elapsed}");

        Assert.Equal([1m], Assert.Single(await QueryAsync(connection, "select 1 from dual")));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Transactions_CommitRollbackAndSavepoint_AreHonoured()
    {
        await using var connection = await OpenAsync();
        await using var observer = await OpenAsync();
        var probe = Assert.IsAssignableFrom<Squill.Data.ITransactionProbe>(connection);
        await RunAsync(connection, "create table squill_tx (id number(10))", CancellationToken.None);
        try
        {
            async Task<decimal> CountAsync(IDatabaseConnection c) => (decimal)(await QueryAsync(c, "select count(*) from squill_tx"))[0][0]!;

            Assert.False((await probe.ProbeAsync(CancellationToken.None)).InTransaction);
            await RunAsync(connection, "SET TRANSACTION READ WRITE", CancellationToken.None);
            await RunAsync(connection, "insert into squill_tx values (1)", CancellationToken.None);
            await RunAsync(connection, "SAVEPOINT sp1", CancellationToken.None);
            await RunAsync(connection, "insert into squill_tx values (2)", CancellationToken.None);
            Assert.True((await probe.ProbeAsync(CancellationToken.None)).InTransaction);
            Assert.Equal(2m, await CountAsync(connection));
            Assert.Equal(0m, await CountAsync(observer));

            await RunAsync(connection, "ROLLBACK TO SAVEPOINT sp1", CancellationToken.None);
            Assert.Equal(1m, await CountAsync(connection));
            await RunAsync(connection, "COMMIT", CancellationToken.None);
            Assert.False((await probe.ProbeAsync(CancellationToken.None)).InTransaction);
            Assert.Equal(1m, await CountAsync(observer));

            await RunAsync(connection, "SET TRANSACTION READ WRITE", CancellationToken.None);
            await RunAsync(connection, "insert into squill_tx values (3)", CancellationToken.None);
            await RunAsync(connection, "ROLLBACK", CancellationToken.None);
            Assert.Equal(1m, await CountAsync(connection));
            Assert.Equal(1m, await CountAsync(observer));

            // Back in auto-commit: a plain insert is visible to the other session at once.
            await RunAsync(connection, "insert into squill_tx values (4)", CancellationToken.None);
            Assert.Equal(2m, await CountAsync(observer));
        }
        finally
        {
            await RunAsync(connection, "drop table squill_tx", CancellationToken.None);
        }
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task ImplicitResults_TwoCursors_AreReadOneAfterTheOther()
    {
        await using var connection = await OpenAsync();

        await using var reader = await connection.ExecuteAsync(
            "declare c1 sys_refcursor; c2 sys_refcursor; begin " +
            "open c1 for select 1 as a from dual; dbms_sql.return_result(c1); " +
            "open c2 for select 'x' as b, 'y' as c from dual; dbms_sql.return_result(c2); end;", [], CancellationToken.None);
        Assert.Equal(["A"], reader.Columns.Select(c => c.Name));
        Assert.Equal([1m], Assert.Single((await reader.ReadAllAsync(10, CancellationToken.None))));

        Assert.True(await reader.NextResultAsync(CancellationToken.None));
        Assert.Equal(["B", "C"], reader.Columns.Select(c => c.Name));
        Assert.Equal(["x", "y"], Assert.Single((await reader.ReadAllAsync(10, CancellationToken.None))));

        Assert.False(await reader.NextResultAsync(CancellationToken.None));
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task DbmsOutput_PutLine_ArrivesAsMessages()
    {
        await using var connection = await OpenAsync();

        await using var reader = await connection.ExecuteAsync(
            "begin dbms_output.put_line('hello'); dbms_output.put_line(''); dbms_output.put_line('world'); end;", [], CancellationToken.None);
        Assert.False(await reader.NextResultAsync(CancellationToken.None));
        Assert.Equal(["hello", "", "world"], reader.Messages);

        await using var again = await connection.ExecuteAsync("select 1 from dual", [], CancellationToken.None);
        await again.ReadAllAsync(10, CancellationToken.None);
        Assert.Empty(again.Messages);
    }
}
