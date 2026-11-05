using Squill.Core;
using Xunit;

namespace Squill.Data.Oracle.Tests;

public sealed class OracleProviderTests
{
    private static ConnectionProfile Profile(string? host = "db.example", string? database = null) =>
        new(Guid.NewGuid(), "ora", DatabaseDialect.Oracle, host, null, database, "scott", null, null);

    private static readonly WireColumn[] TwoColumns = [new("ID", "NUMBER", "i"), new("NAME", "VARCHAR2", "s")];

    private static WireCell?[] Row(int id, string? name) => [WireValue.Encode(id), name is null ? null : new WireCell("s", name)];

    private static async Task<IDatabaseConnection> OpenAsync(FakeBridge bridge, ConnectionProfile? profile = null) =>
        await new OracleDatabaseProvider(() => bridge.Client).OpenAsync(profile ?? Profile(), "tiger", CancellationToken.None);

    [Fact]
    public async Task OpenAsync_Profile_SendsConnectionFieldsAndReturnsServerVersion()
    {
        await using var bridge = new FakeBridge(FakeBridge.Scripted([]));
        await using var connection = await OpenAsync(bridge);

        Assert.Equal("Oracle Database 23ai", connection.ServerVersion);
        var open = Assert.Single(bridge.Requests);
        Assert.Equal(("open", "db.example", 1521, OracleDatabaseProvider.DefaultService, "scott", "tiger"), (open.Op, open.Host, open.Port, open.Service, open.User, open.Password));
    }

    [Fact]
    public async Task OpenAsync_NoHost_Throws()
    {
        await using var bridge = new FakeBridge(FakeBridge.Scripted([]));
        await Assert.ThrowsAsync<ArgumentException>(async () => await OpenAsync(bridge, Profile(host: null)));
    }

    [Fact]
    public async Task OpenAsync_BridgeRefuses_ThrowsAndStopsBridge()
    {
        await using var bridge = new FakeBridge(_ => new BridgeResponse(0, false, "ORA-01017: invalid username/password; logon denied"));
        var e = await Assert.ThrowsAsync<OracleBridgeException>(async () => await OpenAsync(bridge));
        Assert.Contains("ORA-01017", e.Message);
    }

    [Fact]
    public async Task ExecuteAsync_Query_ReturnsTypedColumnsAndBatches()
    {
        var sets = new Dictionary<string, (WireColumn[], WireCell?[][])> { ["from t"] = (TwoColumns, [Row(1, "a"), Row(2, null), Row(3, "c"), Row(4, "d"), Row(5, "e")]) };
        await using var bridge = new FakeBridge(FakeBridge.Scripted(sets));
        await using var connection = await OpenAsync(bridge);

        await using var reader = await connection.ExecuteAsync("select * from t", [], CancellationToken.None);
        Assert.Equal(["ID", "NAME"], reader.Columns.Select(c => c.Name));
        Assert.Equal([typeof(long), typeof(string)], reader.Columns.Select(c => c.ClrType));
        Assert.Equal("NUMBER", reader.Columns[0].DataTypeName);
        Assert.Equal(-1, reader.RecordsAffected);

        var sizes = new List<int>();
        var all = new List<object?[]>();
        while (await reader.ReadBatchAsync(2, CancellationToken.None) is { } batch)
        {
            sizes.Add(batch.Length);
            all.AddRange(batch);
        }
        Assert.Equal([2, 2, 1], sizes);
        Assert.Equal([2L, null], all[1]);
        Assert.Equal(5, reader.RecordsAffected);
    }

    [Fact]
    public async Task ExecuteAsync_Parameters_AreEncodedWithTheirTypes()
    {
        await using var bridge = new FakeBridge(FakeBridge.Scripted(new() { ["x"] = ([], []) }));
        await using var connection = await OpenAsync(bridge);

        await using (await connection.ExecuteAsync("x :a :b :c", [new("a", 7L), new("b", null), new("c", "s")], CancellationToken.None))
        {
        }

        var execute = bridge.Requests.Single(r => r.Op == "execute");
        Assert.Equal([new WireCell("i", "7"), null, new WireCell("s", "s")], execute.Parameters!.Select(p => p.Value));
        Assert.Equal(["a", "b", "c"], execute.Parameters!.Select(p => p.Name));
    }

    [Fact]
    public async Task ExecuteAsync_StatementWithoutResultSet_ReportsRecordsAffected()
    {
        await using var bridge = new FakeBridge(r => r.Op == "execute"
            ? new BridgeResponse(0, true, Columns: [], Done: true, RecordsAffected: 3)
            : new BridgeResponse(0, true, ServerVersion: "v"));
        await using var connection = await OpenAsync(bridge);

        await using var reader = await connection.ExecuteAsync("delete from t", [], CancellationToken.None);
        Assert.Empty(reader.Columns);
        Assert.Equal(3, reader.RecordsAffected);
        Assert.Null(await reader.ReadBatchAsync(10, CancellationToken.None));
        Assert.DoesNotContain("fetch", bridge.Ops);
    }

    [Fact]
    public async Task ExecuteAsync_ServerError_ThrowsWithOracleMessage()
    {
        await using var bridge = new FakeBridge(FakeBridge.Scripted([]));
        await using var connection = await OpenAsync(bridge);

        var e = await Assert.ThrowsAsync<OracleBridgeException>(async () => await connection.ExecuteAsync("select 1 from missing", [], CancellationToken.None));
        Assert.StartsWith("ORA-00942", e.Message);
    }

    [Fact]
    public async Task DisposeAsync_UnfinishedReader_ClosesItOnTheBridge()
    {
        var sets = new Dictionary<string, (WireColumn[], WireCell?[][])> { ["from t"] = (TwoColumns, [Row(1, "a"), Row(2, "b"), Row(3, "c")]) };
        await using var bridge = new FakeBridge(FakeBridge.Scripted(sets));
        await using var connection = await OpenAsync(bridge);

        var reader = await connection.ExecuteAsync("select * from t", [], CancellationToken.None);
        Assert.Single((await reader.ReadBatchAsync(1, CancellationToken.None))!);
        await reader.DisposeAsync();

        Assert.Equal(["open", "execute", "fetch", "closeReader"], bridge.Ops);
    }

    [Fact]
    public async Task DisposeAsync_Connection_SendsClose()
    {
        await using var bridge = new FakeBridge(FakeBridge.Scripted([]));
        var connection = await OpenAsync(bridge);
        await connection.DisposeAsync();

        Assert.Equal(["open", "close"], bridge.Ops);
        Assert.False(bridge.Killed);
    }

    [Fact]
    public async Task ExecuteAsync_Cancelled_KillsTheBridge()
    {
        await using var bridge = new FakeBridge(r => r.Op == "execute" ? null : new BridgeResponse(0, true, ServerVersion: "v"));
        await using var connection = await OpenAsync(bridge);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await connection.ExecuteAsync("select slow", [], cts.Token));

        Assert.True(bridge.Killed);
        await Assert.ThrowsAsync<OracleBridgeException>(async () => await connection.ExecuteAsync("select 1 from dual", [], CancellationToken.None));
    }

    [Fact]
    public async Task ReadSchemaAsync_CatalogRows_BuildTablesColumnsAndPrimaryKeys()
    {
        WireColumn[] one = [new("X", "VARCHAR2", "s")];
        WireCell Text(string v) => new("s", v);
        WireCell Num(int v) => WireValue.Encode(v)!;
        var sets = new Dictionary<string, (WireColumn[], WireCell?[][])>
        {
            ["sys_context"] = (one, [[Text("HR")]]),
            ["from all_constraints"] = (one, [[Text("HR"), Text("EMP"), Text("EMP_PK"), Text("ID")]]),
            ["from all_tab_columns"] = (one,
            [
                [Text("HR"), Text("EMP"), Text("ID"), Text("NUMBER"), null, Num(10), Num(0), Text("N")],
                [Text("HR"), Text("EMP"), Text("NAME"), Text("VARCHAR2"), Num(40), null, null, Text("Y")],
                [Text("HR"), Text("EMP"), Text("SALARY"), Text("NUMBER"), null, Num(8), Num(2), Text("Y")],
                [Text("HR"), Text("EMP"), Text("HIRED"), Text("DATE"), Num(7), null, null, Text("Y")],
                [Text("HR"), Text("EMP_V"), Text("ID"), Text("NUMBER"), null, null, null, Text("Y")],
            ]),
            ["from all_objects"] = (one, [[Text("HR"), Text("EMP"), Text("TABLE")], [Text("HR"), Text("EMP_V"), Text("VIEW")]]),
        };
        await using var bridge = new FakeBridge(FakeBridge.Scripted(sets));
        await using var connection = await OpenAsync(bridge);

        var schema = await connection.ReadSchemaAsync(CancellationToken.None);

        Assert.Equal("HR", schema.DefaultSchema);
        var emp = Assert.IsType<TableInfo>(schema.FindTable("HR", "EMP"));
        Assert.Equal(TableKind.Table, emp.Kind);
        Assert.Equal("EMP_PK", emp.PrimaryKeyName);
        Assert.Equal(["NUMBER(10)", "VARCHAR2(40)", "NUMBER(8,2)", "DATE"], emp.Columns.Select(c => c.DataType));
        Assert.Equal([true, false, false, false], emp.Columns.Select(c => c.IsPrimaryKey));
        Assert.Equal([false, true, true, true], emp.Columns.Select(c => c.IsNullable));
        Assert.Equal(TableKind.View, schema.FindTable("HR", "EMP_V")!.Kind);
    }

    [Fact]
    public async Task ListDatabasesAsync_Schemas_AreReturned()
    {
        WireColumn[] one = [new("USERNAME", "VARCHAR2", "s")];
        await using var bridge = new FakeBridge(FakeBridge.Scripted(new() { ["from all_users"] = (one, [[new WireCell("s", "HR")], [new WireCell("s", "SCOTT")]]) }));
        await using var connection = await OpenAsync(bridge);

        Assert.Equal(["HR", "SCOTT"], await connection.ListDatabasesAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("11.2.0.2.0", 11)]
    [InlineData("23.0.0.0.0", 23)]
    [InlineData("Oracle Database 19c", 0)]
    [InlineData("", 0)]
    public void MajorVersion_VersionString_ParsesLeadingNumber(string version, int expected) =>
        Assert.Equal(expected, OracleSchemaReader.MajorVersion(version));

    [Theory]
    [InlineData("11.2.0.2.0", false)]
    [InlineData("19.0.0.0.0", true)]
    public async Task ReadSchemaAsync_ServerVersion_ChoosesTheSchemaFilter(string version, bool usesOracleMaintained)
    {
        await using var bridge = new FakeBridge(FakeBridge.Scripted(new() { ["sys_context"] = ([new WireColumn("X", "VARCHAR2", "s")], [[new WireCell("s", "HR")]]), ["from all_"] = ([], []) }, version));
        await using var connection = await OpenAsync(bridge);

        await connection.ReadSchemaAsync(CancellationToken.None);

        var catalogQueries = bridge.Requests.Where(r => r.Op == "execute" && r.Sql!.Contains("from all_", StringComparison.Ordinal)).Select(r => r.Sql!).ToList();
        Assert.NotEmpty(catalogQueries);
        Assert.All(catalogQueries, q => Assert.Equal(usesOracleMaintained, q.Contains("oracle_maintained", StringComparison.Ordinal)));
    }

    [Fact]
    public void Registry_OracleProvider_ServesTheOracleDialect()
    {
        var registry = new ProviderRegistry();
        var provider = new OracleDatabaseProvider();
        registry.Register(provider);

        Assert.Same(provider, registry.Get(DatabaseDialect.Oracle));
    }

    [Fact]
    public void ResolvePath_MissingBridge_ThrowsWithTheVariableName()
    {
        var previous = Environment.GetEnvironmentVariable(BridgeClient.PathVariable);
        Environment.SetEnvironmentVariable(BridgeClient.PathVariable, Path.Combine(Path.GetTempPath(), "no-such-squill-bridge.exe"));
        try
        {
            var e = Assert.Throws<FileNotFoundException>(BridgeClient.ResolvePath);
            Assert.Contains(BridgeClient.PathVariable, e.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable(BridgeClient.PathVariable, previous);
        }
    }
}
