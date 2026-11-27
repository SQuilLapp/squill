using System.Globalization;
using Xunit;

namespace Squill.Data.Informix.Tests;

public sealed class OdbcValuesTests
{
    private static async Task<object?[]> OneRowAsync(FakeOdbcApi api)
    {
        using var connection = await OdbcConnection.OpenAsync("cs", api, TestContext.Current.CancellationToken);
        using var reader = await connection.ExecuteAsync("select", TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        return [.. Enumerable.Range(0, reader.FieldCount).Select(reader.GetValue)];
    }

    private static FakeResult Result(params string[] columns) => new(columns, []);

    [Fact]
    public async Task Types_AreConvertedByTheirOdbcCode()
    {
        short[] types = [4, 5, -5, 3, 2, 8, 7, 93, 91, 92, 12, 1, -1, -8, 40, -7, 9999, 3];
        string?[] cells = ["7", "-3", "9000000000", "12.50", "-0.001", "1.5", "2.5", "2026-11-27 10:11:12.12345", "2026-11-27", "13:14:15", "v", "c", "lv", "n", "clob", "1", "odd", null];
        var api = new FakeOdbcApi { Results = { new FakeResult([.. types.Select((_, i) => "c" + i)], [cells], Types: types) } };

        var row = await OneRowAsync(api);

        Assert.Equal(7, row[0]);
        Assert.Equal((short)-3, row[1]);
        Assert.Equal(9_000_000_000L, row[2]);
        Assert.Equal(12.50m, row[3]);
        Assert.Equal(-0.001m, row[4]);
        Assert.Equal(1.5, row[5]);
        Assert.Equal(2.5f, row[6]);
        Assert.Equal(new DateTime(2026, 11, 27, 10, 11, 12).AddTicks(1_234_500), row[7]);
        Assert.Equal(new DateTime(2026, 11, 27), row[8]);
        Assert.Equal(new TimeSpan(13, 14, 15), row[9]);
        Assert.Equal(["v", "c", "lv", "n", "clob"], row[10..15]);
        Assert.Equal(true, row[15]);
        Assert.Equal("odd", row[16]);
        Assert.Null(row[17]);
    }

    [Theory]
    [InlineData("12345678901234567890123456789012")] // 32 digits do not fit a decimal
    [InlineData("0.1234567890123456789012345678901234")] // would silently round
    [InlineData("79228162514264337593543950335")] // 29 digits: stays text on purpose (28-digit limit)
    [InlineData("not a number")]
    public void Decimal_ThatDoesNotFit_StaysText(string text) => Assert.Equal(text, OdbcValue.Convert(3, text));

    [Theory]
    [InlineData("1234567890123456789.12345678", "1234567890123456789.12345678")]
    [InlineData("-0.5", "-0.5")]
    [InlineData("1234567890,12345", "1234567890.12345")] // a comma locale: not a thousands separator
    [InlineData("3,14159000000000E+000", "3.14159")]
    [InlineData("1E+3", "1000")]
    public void Decimal_RoundTrips(string text, string expected) =>
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), OdbcValue.Convert(3, text));

    [Fact]
    public void Datetime_ThatIsNotATimestamp_StaysText() => Assert.Equal("x:y", OdbcValue.Convert(93, "x:y"));

    [Theory]
    [InlineData(1)] // one chunk
    [InlineData(4096)] // exactly the buffer
    [InlineData(10_000)] // 4096 + 4096 + 1808: three chunks
    public async Task Binary_ComesInChunks(int length)
    {
        var bytes = Enumerable.Range(0, length).Select(i => (byte)i).ToArray();
        var api = new FakeOdbcApi { Results = { new FakeResult(["b", "n"], [[null, "x"]], Types: [-4, 12], Blobs: new() { [(0, 0)] = bytes }) } };

        var row = await OneRowAsync(api);

        Assert.Equal(bytes, row[0]);
        Assert.Equal("x", row[1]);
    }

    [Fact]
    public async Task Binary_Empty_IsAnEmptyArray()
    {
        var api = new FakeOdbcApi { Results = { new FakeResult(["b"], [[null]], Types: [-4], Blobs: new() { [(0, 0)] = [] }) } };

        Assert.Equal(Array.Empty<byte>(), (await OneRowAsync(api))[0]);
    }

    [Fact]
    public void Parameters_AreBoundAsTextOrBytes()
    {
        var p = OdbcParameter.From("héllo");
        Assert.Equal((OdbcParameter.CWChar, OdbcParameter.SqlVarchar, 5), (p.CType, p.SqlType, p.ColumnSize));
        Assert.Equal("héllo", p.Text);

        var n = OdbcParameter.From(null);
        Assert.Equal(-1, n.Indicator[0]);
        Assert.Null(n.Text);

        var d = OdbcParameter.From(12.340m);
        Assert.Equal((OdbcParameter.SqlDecimal, 5, 3, "12.340"), (d.SqlType, d.ColumnSize, d.DecimalDigits, d.Text));

        var t = OdbcParameter.From(new DateTime(2026, 11, 27, 1, 2, 3).AddTicks(1_234_567));
        Assert.Equal((OdbcParameter.SqlTimestamp, "2026-11-27 01:02:03.12345"), (t.SqlType, t.Text));

        var b = OdbcParameter.From(new byte[] { 1, 2, 3 });
        Assert.Equal((OdbcParameter.CBinary, 3L), (b.CType, b.Indicator[0]));
        Assert.Equal([1, 2, 3], b.Data[..3]);

        Assert.Equal("1.5", OdbcParameter.From(1.5).Text);
        Assert.Equal("t", OdbcParameter.From(true).Text);
    }

    [Fact]
    public async Task Execute_WithParameters_PreparesBindsInOrderAndExecutes()
    {
        var api = new FakeOdbcApi { Results = { new FakeResult([], [], 1) } };
        using var connection = await OdbcConnection.OpenAsync("cs", api, TestContext.Current.CancellationToken);
        using var reader = await connection.ExecuteAsync("insert into t values (?, ?)", ["a", 5], TestContext.Current.CancellationToken);

        Assert.Equal(["prepare insert into t values (?, ?)"], api.Statements);
        Assert.Equal(["a", "5"], api.Bound.Select(p => p.Text));
        Assert.Equal(1, reader.RowsAffected);
    }

    [Theory]
    [InlineData("select * from t where a = @a and b = @b and c = @a", "select * from t where a = ? and b = ? and c = ?", new[] { "a", "b", "a" })]
    [InlineData("select '@a ?', \"@b\" from t -- @c ?\n where x = @d", "select '@a ?', \"@b\" from t -- @c ?\n where x = ?", new[] { "d" })]
    [InlineData("select /* @a */ { @b } @c_1 from t", "select /* @a */ { @b } ? from t", new[] { "c_1" })]
    [InlineData("select 'it''s @a', @x", "select 'it''s @a', ?", new[] { "x" })]
    [InlineData("select a@b, @1, @", "select a?, @1, @", new[] { "b" })]
    public void Rewrite_ReplacesNamesInOrder_AndLeavesLiteralsAndComments(string sql, string expected, string[] names)
    {
        var (text, found) = PositionalParameters.Rewrite(sql);

        Assert.Equal(expected, text);
        Assert.Equal(names, found);
    }

    [Fact]
    public async Task Cancel_WhileExecuting_CallsSqlCancelOnce_AndTheConnectionStaysUsable()
    {
        using var gate = new ManualResetEventSlim();
        var api = new FakeOdbcApi { BlockUntilCancelled = gate, Results = { Result("a") } };
        using var connection = await OdbcConnection.OpenAsync("cs", api, TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();

        var running = connection.ExecuteAsync("slow", cts.Token);
        Assert.True(api.Started.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        await cts.CancelAsync();
        var e = await Assert.ThrowsAsync<OdbcException>(async () => await running);

        Assert.Equal("HY008", e.SqlState);
        Assert.Equal(1, api.CancelCount);
        Assert.Equal("free stmt", api.Log[^1]);
        api.BlockUntilCancelled = null;
        api.Diagnostics.Clear();
        using var next = await connection.ExecuteAsync("select", TestContext.Current.CancellationToken);
        Assert.Equal(1, next.FieldCount);
    }

    [Fact]
    public async Task Cancel_AfterTheStatementEnded_DoesNotCallSqlCancel()
    {
        var api = new FakeOdbcApi { Results = { Result("a") } };
        using var connection = await OdbcConnection.OpenAsync("cs", api, TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource();
        using var reader = await connection.ExecuteAsync("select", cts.Token);

        await cts.CancelAsync();

        Assert.Equal(0, api.CancelCount);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await reader.ReadAsync(cts.Token));
    }

    [Fact]
    public async Task Transactions_FollowAutocommitAndEndTran()
    {
        var api = new FakeOdbcApi();
        using var connection = await OdbcConnection.OpenAsync("cs", api, TestContext.Current.CancellationToken);
        var ct = TestContext.Current.CancellationToken;

        await connection.CommitAsync(ct); // nothing open: nothing happens
        Assert.False(connection.InTransaction);
        await connection.BeginAsync(ct);
        await connection.BeginAsync(ct); // idempotent
        Assert.True(connection.InTransaction);
        await connection.CommitAsync(ct);
        await connection.BeginAsync(ct);
        await connection.RollbackAsync(ct);

        Assert.Equal(["attr 102=0", "commit", "attr 102=1", "attr 102=0", "rollback", "attr 102=1"], api.Log.Skip(3));
        Assert.False(connection.InTransaction);
    }

    [Fact]
    public async Task Transactions_FailedBegin_LeavesNoTransaction_AndFailedCommitKeepsIt()
    {
        var api = new FakeOdbcApi { TransactionReturn = -1 };
        api.Diagnostics.Add(("HY000", -256, "Transaction not supported"));
        using var connection = await OdbcConnection.OpenAsync("cs", api, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<OdbcException>(async () => await connection.BeginAsync(TestContext.Current.CancellationToken));
        Assert.False(connection.InTransaction);

        api.TransactionReturn = 0;
        await connection.BeginAsync(TestContext.Current.CancellationToken);
        api.TransactionReturn = -1;
        await Assert.ThrowsAsync<OdbcException>(async () => await connection.CommitAsync(TestContext.Current.CancellationToken));
        Assert.True(connection.InTransaction);
    }

    [Fact]
    public async Task Dispose_WithAnOpenTransaction_RollsBackBeforeDisconnect()
    {
        var api = new FakeOdbcApi();
        var connection = await OdbcConnection.OpenAsync("cs", api, TestContext.Current.CancellationToken);
        await connection.BeginAsync(TestContext.Current.CancellationToken);
        connection.Dispose();

        Assert.Equal(["rollback", "disconnect", "free dbc", "free env"], api.Log.Skip(4));
    }

    [Theory]
    [InlineData("begin", 1)]
    [InlineData("BEGIN WORK;", 1)]
    [InlineData(" commit work ", 2)]
    [InlineData("Rollback", 3)]
    [InlineData("rollback to savepoint x", null)]
    [InlineData("begin work now", null)]
    [InlineData("select 1", null)]
    [InlineData("", null)]
    public void TransactionStatement_IsRecognised(string sql, int? expected) =>
        Assert.Equal(expected, InformixTransactionStatement.TryParse(sql));
}
