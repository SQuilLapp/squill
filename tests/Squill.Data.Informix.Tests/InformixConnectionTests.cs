using Xunit;

namespace Squill.Data.Informix.Tests;

/// <summary>The shared connection (typed values, parameters, cancel, transactions) against a real Informix developer server.</summary>
public sealed class InformixConnectionTests(InformixFixture server) : IClassFixture<InformixFixture>
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static void RequireDriver() =>
        Assert.SkipWhen(InformixFixture.FindClidriver() is null,
            "IBM clidriver not found: set SQUILL_CLIDRIVER or restore the Net.IBM.Data.Db2 package (see InformixFixture).");

    private Task<InformixDatabaseConnection> OpenAsync(string database = InformixFixture.Database) =>
        InformixDatabaseConnection.OpenAsync(server.ConnectionString(database: database), Token);

    private static async Task<List<object?[]>> RowsAsync(InformixDatabaseConnection c, string sql, params QueryParameter[] parameters)
    {
        await using var reader = await c.ExecuteAsync(sql, parameters, Token);
        return await reader.ReadAllAsync(500, Token);
    }

    private static async Task RunAsync(InformixDatabaseConnection c, string sql, params QueryParameter[] parameters)
    {
        await using var reader = await c.ExecuteAsync(sql, parameters, Token);
        while (await reader.ReadBatchAsync(500, Token) is not null)
        {
        }
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Stream_TenThousandRows_ThenCancelHalfWay_ThenRunAnotherStatement()
    {
        RequireDriver();
        await using var connection = await OpenAsync();
        const string Sql = "select first 10000 a.tabid from systables a, systables b";

        await using (var all = await connection.ExecuteAsync(Sql, [], Token))
            Assert.Equal(10_000, (await all.ReadAllAsync(1000, Token)).Count);

        using var cts = new CancellationTokenSource();
        await using (var half = await connection.ExecuteAsync(Sql, [], cts.Token))
        {
            for (var i = 0; i < 5; i++)
                Assert.NotNull(await half.ReadBatchAsync(1000, cts.Token));
            await cts.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await half.ReadBatchAsync(1000, cts.Token));
        }

        Assert.Equal(1, (await RowsAsync(connection, "select first 1 1 from systables"))[0][0]);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Cancel_LongRunningStatement_StopsItAndTheConnectionStaysUsable()
    {
        RequireDriver();
        await using var connection = await OpenAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var watch = System.Diagnostics.Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await using var reader = await connection.ExecuteAsync("select count(*) from systables a, systables b, systables c, systables d", [], cts.Token);
            await reader.ReadAllAsync(10, cts.Token);
        });

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), "the cancel took " + watch.Elapsed);
        Assert.Equal(1, (await RowsAsync(connection, "select first 1 1 from systables"))[0][0]);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Parameters_RoundTripTypedValues_AndNull()
    {
        RequireDriver();
        await using var connection = await OpenAsync(InformixFixture.LoggedDatabase);
        await RunAsync(connection, "create table rt (id integer, d decimal(20,5), ts datetime year to fraction(5), lv lvarchar(2000), b byte, n varchar(10), big bigint, sm smallint, f float, dt date, h datetime hour to second, ymd datetime year to day, flag boolean)");
        var blob = Enumerable.Range(0, 10_000).Select(i => (byte)(i * 7)).ToArray();
        var stamp = new DateTime(2026, 11, 27, 10, 11, 12).AddTicks(1_234_500);
        var longText = new string('é', 1500);

        await RunAsync(connection, "insert into rt (id, d, ts, lv, b, n) values (@id, @d, @ts, @lv, @b, @n)",
            new("id", 42), new("d", 1234567890.12345m), new("ts", stamp), new("lv", longText), new("b", blob), new("n", null));
        await RunAsync(connection, "insert into rt (id, big, sm, f, dt, h, ymd, flag) values (100, 9000000000, -7, 1.5, '2026-11-27', '10:11:12', '2026-11-27', 't')");
        await using var reader = await connection.ExecuteAsync("select id, d, ts, lv, b, n, big, sm, f, dt, h, ymd, flag from rt order by id", [], Token);
        var types = string.Join(",", reader.Columns.Select(c => c.DataTypeName));
        var rows = await reader.ReadAllAsync(10, Token);

        Assert.Equal("INTEGER,DECIMAL,DATETIME,LVARCHAR,BLOB,VARCHAR,BIGINT,SMALLINT,DOUBLE,DATE,TIME,DATE,SMALLINT", types);
        Assert.Equal(42, rows[0][0]);
        Assert.Equal(1234567890.12345m, rows[0][1]);
        Assert.Equal(stamp, rows[0][2]);
        Assert.Equal(longText, rows[0][3]);
        Assert.Equal(blob, rows[0][4]);
        Assert.Null(rows[0][5]);
        Assert.Equal(9_000_000_000L, rows[1][6]);
        Assert.Equal((short)-7, rows[1][7]);
        Assert.Equal(1.5, rows[1][8]);
        Assert.Equal(new DateTime(2026, 11, 27), rows[1][9]);
        Assert.Equal(new TimeSpan(10, 11, 12), rows[1][10]);
        Assert.Equal(new DateTime(2026, 11, 27), rows[1][11]);
        Assert.Equal((short)1, rows[1][12]); // BOOLEAN arrives as SMALLINT
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Transactions_ManualCommitAndRollback_InALoggedDatabase()
    {
        RequireDriver();
        await using var connection = await OpenAsync(InformixFixture.LoggedDatabase);
        await using var other = await OpenAsync(InformixFixture.LoggedDatabase);
        await RunAsync(connection, "create table tx (id integer)");

        await RunAsync(connection, "insert into tx values (1)"); // autocommit is on by default
        Assert.Equal(1, (await RowsAsync(other, "select count(*) from tx"))[0][0]);
        Assert.False((await connection.ProbeAsync(Token)).InTransaction);

        await RunAsync(connection, "BEGIN");
        Assert.True((await connection.ProbeAsync(Token)).InTransaction);
        await RunAsync(connection, "insert into tx values (2)");
        await RunAsync(connection, "ROLLBACK");
        Assert.False((await connection.ProbeAsync(Token)).InTransaction);
        Assert.Equal(1, (await RowsAsync(connection, "select count(*) from tx"))[0][0]);

        await RunAsync(connection, "BEGIN");
        await RunAsync(connection, "insert into tx values (3)");
        await RunAsync(connection, "COMMIT");
        Assert.Equal(2, (await RowsAsync(other, "select count(*) from tx"))[0][0]);
        await RunAsync(connection, "insert into tx values (4)"); // autocommit is back
        Assert.Equal(3, (await RowsAsync(other, "select count(*) from tx"))[0][0]);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Transactions_InADatabaseWithoutLog_AreReportedAsUnsupported()
    {
        RequireDriver();
        await using var connection = await OpenAsync(InformixFixture.UnloggedDatabase);
        await RunAsync(connection, "create table tx (id integer)");

        var e = await Assert.ThrowsAsync<OdbcException>(async () =>
        {
            await RunAsync(connection, "BEGIN");
            await RunAsync(connection, "insert into tx values (1)");
            await RunAsync(connection, "COMMIT");
        });

        Assert.NotEqual("", e.SqlState);
        Assert.False((await connection.ProbeAsync(Token)).InTransaction ?? false, e.Message);
    }

    [Fact(Skip = DockerGate.SkipReason, SkipUnless = nameof(DockerGate.Enabled), SkipType = typeof(DockerGate))]
    public async Task Error_ReportsTheSqlState_AndServerVersionIsRead()
    {
        RequireDriver();
        await using var connection = await OpenAsync();
        var e = await Assert.ThrowsAsync<OdbcException>(async () => await RunAsync(connection, "select * from no_such_table"));

        Assert.Equal("42S02", e.SqlState, StringComparer.Ordinal);
        Assert.Contains("Informix", connection.ServerVersion, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sqlog", await connection.ListDatabasesAsync(Token));
    }
}
