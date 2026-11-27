using Xunit;

namespace Squill.Data.Informix.Tests;

public sealed class OdbcLayerTests
{
    private static FakeResult Result(string[] columns, params string?[][] rows) => new(columns, [.. rows]);

    private static async Task<List<string?[]>> ReadAllAsync(OdbcTextReader reader)
    {
        var rows = new List<string?[]>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            rows.Add(Enumerable.Range(0, reader.FieldCount).Select(reader.GetText).ToArray());
        return rows;
    }

    [Fact]
    public async Task Execute_ReadsColumnsRowsAndNull()
    {
        var api = new FakeOdbcApi { Results = { Result(["a", "b"], ["x", null], ["", "y"]) } };
        using var connection = await OdbcConnection.OpenAsync("cs", api, TestContext.Current.CancellationToken);
        using var reader = await connection.ExecuteAsync("select", TestContext.Current.CancellationToken);

        Assert.Equal(2, reader.FieldCount);
        Assert.Equal("b", reader.ColumnName(1));
        Assert.Equal(12, reader.ColumnSqlType(0));
        var rows = await ReadAllAsync(reader);
        Assert.Equal(["x", null], rows[0]);
        Assert.Equal(["", "y"], rows[1]);
        Assert.False(await reader.ReadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Execute_SeveralResultSets_AreReadInTurn()
    {
        var api = new FakeOdbcApi { Results = { Result(["a"], ["1"]), new FakeResult([], [], 7), Result(["b", "c"], ["2", "3"]) } };
        using var connection = await OdbcConnection.OpenAsync("cs", api, TestContext.Current.CancellationToken);
        using var reader = await connection.ExecuteAsync("multi", TestContext.Current.CancellationToken);

        Assert.Equal([["1"]], await ReadAllAsync(reader));
        Assert.True(await reader.NextResultAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, reader.FieldCount);
        Assert.Equal(7, reader.RowsAffected);
        Assert.False(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.True(await reader.NextResultAsync(TestContext.Current.CancellationToken));
        Assert.Equal([["2", "3"]], await ReadAllAsync(reader));
        Assert.False(await reader.NextResultAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, reader.FieldCount);
    }

    [Fact]
    public async Task Execute_NoData_IsNotAnError()
    {
        var api = new FakeOdbcApi { ExecReturn = 100, Results = { new FakeResult([], [], 0) } };
        using var connection = await OdbcConnection.OpenAsync("cs", api, TestContext.Current.CancellationToken);
        using var reader = await connection.ExecuteAsync("update t set a = 1 where 1 = 0", TestContext.Current.CancellationToken);

        Assert.Equal(0, reader.RowsAffected);
        Assert.False(await reader.ReadAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(1500, false)]
    [InlineData(3000, false)]
    [InlineData(1500, true)]
    public async Task GetText_ValueLongerThanBuffer_ComesInChunks(int length, bool noTotal)
    {
        var value = new string('x', length - 1) + "é";
        var api = new FakeOdbcApi { ReportNoTotal = noTotal, Results = { Result(["a", "b"], [value, "next"]) } };
        using var connection = await OdbcConnection.OpenAsync("cs", api, TestContext.Current.CancellationToken);
        using var reader = await connection.ExecuteAsync("long", TestContext.Current.CancellationToken);

        Assert.Equal([[value, "next"]], await ReadAllAsync(reader));
    }

    [Fact]
    public async Task GetText_ValueFillingTheBufferExactly_IsNotLost()
    {
        var value = new string('y', 1023);
        var api = new FakeOdbcApi { Results = { Result(["a"], [value]) } };
        using var connection = await OdbcConnection.OpenAsync("cs", api, TestContext.Current.CancellationToken);
        using var reader = await connection.ExecuteAsync("edge", TestContext.Current.CancellationToken);

        Assert.Equal([[value]], await ReadAllAsync(reader));
    }

    [Fact]
    public async Task Error_JoinsEveryDiagnosticRecord_AndFreesTheStatement()
    {
        var api = new FakeOdbcApi { FailAt = "Exec", Results = { Result(["a"]) } };
        api.Diagnostics.Add(("42S02", -206, "table not found"));
        api.Diagnostics.Add(("01000", 0, "second record"));
        using var connection = await OdbcConnection.OpenAsync("cs", api, TestContext.Current.CancellationToken);

        var e = await Assert.ThrowsAsync<OdbcException>(async () => await connection.ExecuteAsync("select", TestContext.Current.CancellationToken));
        Assert.Equal("42S02", e.SqlState);
        Assert.Equal(-206, e.NativeError);
        Assert.Equal("[42S02] table not found (-206)\n[01000] second record (0)", e.Message);
        Assert.Equal("free stmt", api.Log[^1]);
    }

    [Fact]
    public async Task SuccessWithInfo_IsNotAnError()
    {
        var api = new FakeOdbcApi { ExecReturn = 1, Results = { Result(["a"], ["1"]) } };
        using var connection = await OdbcConnection.OpenAsync("cs", api, TestContext.Current.CancellationToken);
        using var reader = await connection.ExecuteAsync("select", TestContext.Current.CancellationToken);

        Assert.Equal([["1"]], await ReadAllAsync(reader));
    }

    [Fact]
    public async Task FetchError_Throws()
    {
        var api = new FakeOdbcApi { FailAt = "Fetch", Results = { Result(["a"], ["1"]) } };
        using var connection = await OdbcConnection.OpenAsync("cs", api, TestContext.Current.CancellationToken);
        using var reader = await connection.ExecuteAsync("select", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<OdbcException>(async () => await reader.ReadAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("AllocEnv", new string[] { "alloc AllocEnv" })]
    [InlineData("SetEnvAttr", new[] { "alloc AllocEnv", "free env" })]
    [InlineData("AllocDbc", new[] { "alloc AllocEnv", "alloc AllocDbc", "free env" })]
    [InlineData("Connect", new[] { "alloc AllocEnv", "alloc AllocDbc", "connect", "free dbc", "free env" })]
    public async Task Open_FailureAtEachStep_ReleasesHandlesInOrder(string failAt, string[] expected)
    {
        var api = new FakeOdbcApi { FailAt = failAt };
        await Assert.ThrowsAsync<OdbcException>(async () => await OdbcConnection.OpenAsync("cs", api, TestContext.Current.CancellationToken));
        Assert.Equal(expected, api.Log);
    }

    [Fact]
    public async Task Dispose_DisconnectsAndFreesInOrder_Once()
    {
        var api = new FakeOdbcApi { Results = { Result(["a"], ["1"]) } };
        var connection = await OdbcConnection.OpenAsync("cs", api, TestContext.Current.CancellationToken);
        var reader = await connection.ExecuteAsync("select", TestContext.Current.CancellationToken);
        reader.Dispose();
        connection.Dispose();
        connection.Dispose();

        Assert.Equal(
            ["alloc AllocEnv", "alloc AllocDbc", "connect", "alloc AllocStmt", "closecursor", "free stmt", "disconnect", "free dbc", "free env"],
            api.Log);
    }

    [Fact]
    public async Task Open_Failure_NeverShowsThePasswordInTheMessage()
    {
        var api = new FakeOdbcApi { FailAt = "Connect" };
        api.Diagnostics.Add(("08001", -30082, "Security processing failed"));
        var connectionString = OdbcConnectionString.Build("h", 9089, "db", "informix", "s3cret");
        var e = await Assert.ThrowsAsync<OdbcException>(async () => await OdbcConnection.OpenAsync(connectionString, api, TestContext.Current.CancellationToken));

        Assert.DoesNotContain("s3cret", e.Message);
        Assert.DoesNotContain("s3cret", e.ToString().Replace(connectionString, ""));
    }

    [Fact]
    public void ConnectionString_IsBuiltAndEscaped()
    {
        Assert.Equal("DATABASE=db;HOSTNAME=h;PORT=9089;PROTOCOL=TCPIP;UID=u;PWD=p", OdbcConnectionString.Build("h", 9089, "db", "u", "p"));
        Assert.EndsWith("PWD={a;b}}c}", OdbcConnectionString.Build("h", 1, "db", "u", "a;b}c"));
        Assert.Contains("UID={{x}", OdbcConnectionString.Build("h", 1, "db", "{x", "p"));
    }

    [Fact]
    public void Locator_PrefersTheVariable_ThenTheAppFolder()
    {
        var withVariable = ClidriverLocator.Locate(_ => Path.Combine("C:", "ibm"), Path.Combine("C:", "app"), _ => true);
        Assert.Equal(Path.Combine("C:", "ibm", "bin", "db2cli64.dll"), withVariable);

        var fromApp = ClidriverLocator.Locate(_ => null, Path.Combine("C:", "app"), _ => true);
        Assert.Equal(Path.Combine("C:", "app", "clidriver", "bin", "db2cli64.dll"), fromApp);
    }

    [Fact]
    public void Locator_MissingFile_NamesTheVariableAndTheFolder()
    {
        var e = Assert.Throws<DllNotFoundException>(() => ClidriverLocator.Locate(_ => Path.Combine("C:", "ibm"), Path.Combine("C:", "app"), _ => false));
        Assert.Contains("SQUILL_CLIDRIVER", e.Message);
        Assert.Contains(Path.Combine("C:", "ibm", "bin"), e.Message);
        Assert.Contains(Path.Combine("C:", "app", "clidriver"), e.Message);
    }
}
