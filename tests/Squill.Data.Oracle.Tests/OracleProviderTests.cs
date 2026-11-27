using Squill.Core;
using Squill.Core.Transactions;
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
    public async Task ExecuteAsync_CancelledWhileBridgeAnswers_KeepsTheConnection()
    {
        await using var bridge = new FakeBridge(r => r.Op == "execute" && r.Sql == "slow" ? null : r.Op == "open" ? new BridgeResponse(0, true, ServerVersion: "v") : new BridgeResponse(0, true, Done: true), honourCancel: true);
        await using var connection = await OpenAsync(bridge);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await connection.ExecuteAsync("slow", [], cts.Token));
        await (await connection.ExecuteAsync("select 1 from dual", [], CancellationToken.None)).DisposeAsync();

        Assert.False(bridge.Killed);
        Assert.Equal(["open", "execute", "cancel", "execute"], bridge.Ops);
        Assert.True(bridge.Requests.Single(r => r.Op == "cancel").Id < 0);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyCancelled_SendsNothing()
    {
        await using var bridge = new FakeBridge(FakeBridge.Scripted([]));
        await using var connection = await OpenAsync(bridge);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await connection.ExecuteAsync("select 1 from dual", [], cts.Token));
        Assert.Equal(["open"], bridge.Ops);
    }

    [Theory]
    [InlineData("SET TRANSACTION READ WRITE", "begin", null)]
    [InlineData("set transaction read write;", "begin", null)]
    [InlineData("COMMIT", "commit", null)]
    [InlineData("commit work;", "commit", null)]
    [InlineData("ROLLBACK", "rollback", null)]
    [InlineData("SAVEPOINT squill_sp_1", "savepoint", "squill_sp_1")]
    [InlineData("ROLLBACK TO SAVEPOINT squill_sp_1", "rollback", "squill_sp_1")]
    [InlineData("rollback to sp;", "rollback", "sp")]
    public async Task ExecuteAsync_TransactionStatement_RunsAsBridgeOperation(string sql, string op, string? name)
    {
        await using var bridge = new FakeBridge(r => r.Op == "open" ? new BridgeResponse(0, true, ServerVersion: "v") : new BridgeResponse(0, true, Done: true));
        await using var connection = await OpenAsync(bridge);

        await using (var reader = await connection.ExecuteAsync(sql, [], CancellationToken.None))
            Assert.Empty(reader.Columns);

        var sent = bridge.Requests[^1];
        Assert.Equal((op, name, null), (sent.Op, sent.Name, sent.Sql));
        Assert.DoesNotContain("closeReader", bridge.Ops);
    }

    [Theory]
    [InlineData("SET TRANSACTION READ ONLY")]
    [InlineData("COMMIT COMMENT 'x'")]
    [InlineData("ROLLBACK TO bad-name")]
    [InlineData("SELECT * FROM commit")]
    public async Task ExecuteAsync_OtherStatement_StaysSql(string sql)
    {
        await using var bridge = new FakeBridge(r => r.Op == "open" ? new BridgeResponse(0, true, ServerVersion: "v") : new BridgeResponse(0, true, Done: true));
        await using var connection = await OpenAsync(bridge);

        await (await connection.ExecuteAsync(sql, [], CancellationToken.None)).DisposeAsync();
        Assert.Equal(("execute", sql), (bridge.Requests[^1].Op, bridge.Requests[^1].Sql));
    }

    [Fact]
    public async Task ExecuteAsync_TransactionError_Throws()
    {
        await using var bridge = new FakeBridge(r => r.Op == "commit" ? new BridgeResponse(0, false, "ORA-02091: transaction rolled back") : new BridgeResponse(0, true, ServerVersion: "v"));
        await using var connection = await OpenAsync(bridge);

        var e = await Assert.ThrowsAsync<OracleBridgeException>(async () => await connection.ExecuteAsync("COMMIT", [], CancellationToken.None));
        Assert.StartsWith("ORA-02091", e.Message);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task ProbeAsync_AnswersFromTheBridge(bool inTransaction, bool expected)
    {
        await using var bridge = new FakeBridge(r => r.Op == "probe" ? new BridgeResponse(0, true, InTransaction: inTransaction) : new BridgeResponse(0, true, ServerVersion: "v"));
        await using var connection = await OpenAsync(bridge);

        var probe = Assert.IsAssignableFrom<ITransactionProbe>(connection);
        Assert.Equal(new TransactionProbeResult(expected, false), await probe.ProbeAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ProbeAsync_BridgeError_IsUnknown()
    {
        await using var bridge = new FakeBridge(r => r.Op == "probe" ? new BridgeResponse(0, false, "ORA-00942") : new BridgeResponse(0, true, ServerVersion: "v"));
        await using var connection = await OpenAsync(bridge);

        Assert.Equal(TransactionProbeResult.Unknown, await ((ITransactionProbe)connection).ProbeAsync(CancellationToken.None));
    }

    [Fact]
    public async Task NextResultAsync_ImplicitResults_MoveAcrossSetsAndCollectMessages()
    {
        WireColumn[] one = [new("A", "NUMBER", "i")];
        WireColumn[] two = [new("B", "VARCHAR2", "s")];
        var nextCalls = 0;
        await using var bridge = new FakeBridge(r => r.Op switch
        {
            "open" => new BridgeResponse(0, true, ServerVersion: "v"),
            "execute" => new BridgeResponse(0, true, Columns: one, Messages: ["from block"]),
            "fetch" => new BridgeResponse(0, true, Rows: [[WireValue.Encode(1)]], Done: true),
            "nextResult" => ++nextCalls switch
            {
                1 => new BridgeResponse(0, true, Columns: two, Messages: ["second", "lines"]),
                _ => new BridgeResponse(0, true, Done: true, RecordsAffected: 4),
            },
            _ => new BridgeResponse(0, true),
        });
        await using var connection = await OpenAsync(bridge);

        await using var reader = await connection.ExecuteAsync("begin dbms_sql.return_result(:c); end;", [], CancellationToken.None);
        Assert.Equal(["A"], reader.Columns.Select(c => c.Name));
        Assert.Single((await reader.ReadBatchAsync(10, CancellationToken.None))!);
        Assert.Null(await reader.ReadBatchAsync(10, CancellationToken.None));
        Assert.Equal(["from block"], reader.Messages);

        Assert.True(await reader.NextResultAsync(CancellationToken.None));
        Assert.Equal(["B"], reader.Columns.Select(c => c.Name));
        Assert.Equal(typeof(string), reader.Columns[0].ClrType);
        Assert.Equal(["from block", "second", "lines"], reader.Messages);

        Assert.False(await reader.NextResultAsync(CancellationToken.None));
        Assert.Empty(reader.Columns);
        Assert.Equal(4, reader.RecordsAffected);
        Assert.False(await reader.NextResultAsync(CancellationToken.None));
        await reader.DisposeAsync();

        Assert.Equal(["open", "execute", "fetch", "nextResult", "nextResult"], bridge.Ops);
    }

    [Fact]
    public async Task NextResultAsync_StatementWithoutResultSet_AnswersLocally()
    {
        await using var bridge = new FakeBridge(r => r.Op == "execute" ? new BridgeResponse(0, true, Done: true, RecordsAffected: 1, Messages: ["hi"]) : new BridgeResponse(0, true, ServerVersion: "v"));
        await using var connection = await OpenAsync(bridge);

        await using var reader = await connection.ExecuteAsync("begin dbms_output.put_line(1); end;", [], CancellationToken.None);
        Assert.False(await reader.NextResultAsync(CancellationToken.None));
        Assert.Equal(["hi"], reader.Messages);
        Assert.DoesNotContain("nextResult", bridge.Ops);
    }

    [Fact]
    public async Task SendAsync_AnswerWithAnotherId_Throws()
    {
        var toBridge = new System.IO.Pipelines.Pipe();
        var fromBridge = new System.IO.Pipelines.Pipe();
        await using var client = new BridgeClient(toBridge.Writer.AsStream(), fromBridge.Reader.AsStream(), _ => { });
        await fromBridge.Writer.WriteAsync(System.Text.Encoding.UTF8.GetBytes("{\"id\":99,\"ok\":true}\n"), TestContext.Current.CancellationToken);

        var e = await Assert.ThrowsAsync<OracleBridgeException>(async () => await client.SendAsync(new BridgeRequest(0, "fetch"), CancellationToken.None));
        Assert.Contains("99", e.Message);
    }

    [Fact]
    public async Task SendAsync_BridgeExits_LaterCallsFail()
    {
        await using var bridge = new FakeBridge(FakeBridge.Scripted([]));
        await using var connection = await OpenAsync(bridge);
        await bridge.Client.DisposeAsync();

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
            ["from dual"] = (one, [[Text("HR")]]),
            ["c.constraint_type = 'P'"] = (one, [[Text("HR"), Text("EMP"), Text("EMP_PK"), Text("ID")]]),
            ["column_id"] = (one,
            [
                [Text("HR"), Text("EMP"), Text("ID"), Text("NUMBER"), null, Num(10), Num(0), Text("N")],
                [Text("HR"), Text("EMP"), Text("NAME"), Text("VARCHAR2"), Num(40), null, null, Text("Y")],
                [Text("HR"), Text("EMP"), Text("SALARY"), Text("NUMBER"), null, Num(8), Num(2), Text("Y")],
                [Text("HR"), Text("EMP"), Text("HIRED"), Text("DATE"), Num(7), null, null, Text("Y")],
                [Text("HR"), Text("EMP_V"), Text("ID"), Text("NUMBER"), null, null, null, Text("Y")],
            ]),
            ["from all_objects"] = (one, [[Text("HR"), Text("EMP"), Text("TABLE")], [Text("HR"), Text("EMP_V"), Text("VIEW")], [Text("HR"), Text("DEPT"), Text("TABLE")], [Text("HR"), Text("EMP_MV"), Text("TABLE")]]),
            ["data_default"] = (one,
            [
                [Text("HR"), Text("EMP"), Text("NAME"), Text("'n/a' ")],
                [Text("HR"), Text("EMP"), Text("SALARY"), Text("NULL")],
                [Text("HR"), Text("EMP"), Text("ID"), Text("\"HR\".\"ISEQ$$_1\".nextval")],
            ]),
            ["from all_tab_identity_cols"] = (one, [[Text("HR"), Text("EMP"), Text("ID")]]),
            ["from all_indexes"] = (one,
            [
                [Text("HR"), Text("EMP"), Text("EMP_NAME_UK"), Text("UNIQUE"), Text("NAME")],
                [Text("HR"), Text("EMP"), Text("EMP_SAL_HIRED"), Text("NONUNIQUE"), Text("SALARY")],
                [Text("HR"), Text("EMP"), Text("EMP_SAL_HIRED"), Text("NONUNIQUE"), Text("HIRED")],
            ]),
            ["c.constraint_type = 'R'"] = (one,
            [
                [Text("HR"), Text("EMP"), Text("EMP_DEPT_FK"), Text("DEPT_ID"), Text("HR"), Text("DEPT"), Text("ID"), Text("CASCADE")],
                [Text("HR"), Text("EMP"), Text("EMP_MGR_FK"), Text("MGR_A"), Text("HR"), Text("EMP"), Text("ID"), Text("SET NULL")],
                [Text("HR"), Text("EMP"), Text("EMP_MGR_FK"), Text("MGR_B"), Text("HR"), Text("EMP"), Text("ID2"), Text("SET NULL")],
                [Text("HR"), Text("EMP"), Text("EMP_X_FK"), Text("X"), Text("HR"), Text("DEPT"), Text("ID"), Text("NO ACTION")],
            ]),
            ["from all_views"] = (one, [[Text("HR"), Text("EMP_V"), Text("select id from emp\n")]]),
            ["from all_mviews"] = (one, [[Text("HR"), Text("EMP_MV"), Text("select id from emp")]]),
            ["from all_procedures"] = (one, [[Text("HR"), Text("RAISE_PAY"), Text("FUNCTION")], [Text("HR"), Text("CLEAN"), Text("PROCEDURE")], [Text("HR"), Text("PAY_PKG"), Text("PACKAGE")]]),
            ["from all_arguments"] = (one,
            [
                [Text("HR"), Text("RAISE_PAY"), null, Text("OUT"), Text("NUMBER"), Num(0)],
                [Text("HR"), Text("RAISE_PAY"), Text("P_ID"), Text("IN"), Text("NUMBER"), Num(1)],
                [Text("HR"), Text("RAISE_PAY"), Text("P_PCT"), Text("IN/OUT"), Text("NUMBER"), Num(2)],
            ]),
            ["from all_source"] = (one,
            [
                [Text("HR"), Text("RAISE_PAY"), Text("FUNCTION"), Text("FUNCTION raise_pay(p_id number, p_pct in out number) return number is\n")],
                [Text("HR"), Text("RAISE_PAY"), Text("FUNCTION"), Text("begin return 1; end;\n")],
                [Text("HR"), Text("EMP_BI"), Text("TRIGGER"), Text("TRIGGER emp_bi before insert on emp\n")],
            ]),
            ["from all_triggers"] = (one, [[Text("HR"), Text("HR"), Text("EMP"), Text("EMP_BI"), Text("BEFORE EACH ROW"), Text("INSERT OR UPDATE")]]),
            ["from all_"] = (one, []),
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
        Assert.Equal("select id from emp", schema.FindTable("HR", "EMP_V")!.Definition);

        Assert.Equal([null, "'n/a'", null, null], emp.Columns.Select(c => c.DefaultExpression));
        Assert.Equal([true, false, false, false], emp.Columns.Select(c => c.IsAutoIncrement));
        Assert.Equal([("EMP_NAME_UK", true, "NAME"), ("EMP_SAL_HIRED", false, "SALARY,HIRED")], emp.Indexes.Select(i => (i.Name, i.IsUnique, string.Join(',', i.Columns))));
        Assert.Equal(["EMP_DEPT_FK", "EMP_MGR_FK", "EMP_X_FK"], emp.ForeignKeys.Select(f => f.Name));
        var mgr = emp.ForeignKeys[1];
        Assert.Equal("MGR_A,MGR_B>HR.EMP(ID,ID2)", $"{string.Join(',', mgr.Columns)}>{mgr.ReferencedSchema}.{mgr.ReferencedTable}({string.Join(',', mgr.ReferencedColumns)})");
        Assert.Equal([ReferentialAction.Cascade, ReferentialAction.SetNull, ReferentialAction.NoAction], emp.ForeignKeys.Select(f => f.OnDelete));
        Assert.Equal(TableKind.MaterializedView, schema.FindTable("HR", "EMP_MV")!.Kind);
        Assert.Equal("select id from emp", schema.FindTable("HR", "EMP_MV")!.Definition);

        Assert.Equal([("RAISE_PAY", RoutineKind.Function, "RAISE_PAY(P_ID NUMBER, IN/OUT P_PCT NUMBER)", "NUMBER"), ("CLEAN", RoutineKind.Procedure, "CLEAN()", null), ("PAY_PKG", RoutineKind.Procedure, "PAY_PKG", null)],
            schema.Routines.Select(r => (r.Name, r.Kind, r.Signature, r.ReturnType)));
        Assert.Equal("CREATE OR REPLACE FUNCTION raise_pay(p_id number, p_pct in out number) return number is\nbegin return 1; end;", schema.Routines[0].Definition);
        Assert.Null(schema.Routines[1].Definition);
        var trigger = Assert.Single(schema.Triggers);
        Assert.Equal(("HR", "EMP", "EMP_BI", "BEFORE", "INSERT OR UPDATE", "CREATE OR REPLACE TRIGGER emp_bi before insert on emp"), (trigger.Schema, trigger.Table, trigger.Name, trigger.Timing, trigger.Events, trigger.Definition));
    }

    [Fact]
    public async Task ListDatabasesAsync_Schemas_AreReturned()
    {
        WireColumn[] one = [new("USERNAME", "VARCHAR2", "s")];
        await using var bridge = new FakeBridge(FakeBridge.Scripted(new() { ["from all_users"] = (one, [[new WireCell("s", "HR")], [new WireCell("s", "SCOTT")]]) }));
        await using var connection = await OpenAsync(bridge);

        Assert.Equal(["HR", "SCOTT"], await connection.ListDatabasesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ReadSchemaAsync_CatalogQueries_KeepCurrentSchemaAndDoNotUseOracleMaintained()
    {
        await using var bridge = new FakeBridge(FakeBridge.Scripted(new() { ["sys_context('USERENV', 'CURRENT_SCHEMA') from dual"] = ([new WireColumn("X", "VARCHAR2", "s")], [[new WireCell("s", "HR")]]), ["from all_"] = ([], []) }));
        await using var connection = await OpenAsync(bridge);

        await connection.ReadSchemaAsync(CancellationToken.None);

        var catalogQueries = bridge.Requests.Where(r => r.Op == "execute" && r.Sql!.Contains("from all_", StringComparison.Ordinal)).Select(r => r.Sql!).ToList();
        Assert.Equal(13, catalogQueries.Count);
        Assert.All(catalogQueries, q =>
        {
            Assert.DoesNotContain("oracle_maintained", q, StringComparison.Ordinal);
            Assert.Contains("= sys_context('USERENV', 'CURRENT_SCHEMA')", q, StringComparison.Ordinal);
        });
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
