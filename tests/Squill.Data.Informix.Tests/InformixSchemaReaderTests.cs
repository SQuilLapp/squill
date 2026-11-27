using Squill.Core;
using Xunit;

namespace Squill.Data.Informix.Tests;

/// <summary>The type decoder and the catalogue-to-snapshot mapping, on canned rows (no server).</summary>
public sealed class InformixSchemaReaderTests
{
    [Theory]
    [InlineData(0, 12, 0, "CHAR(12)")]
    [InlineData(0, -32768, 0, "CHAR(32768)")]
    [InlineData(1, 2, 0, "SMALLINT")]
    [InlineData(2, 4, 0, "INTEGER")]
    [InlineData(3, 8, 0, "FLOAT")]
    [InlineData(4, 4, 0, "SMALLFLOAT")]
    [InlineData(5, 20 * 256 + 5, 0, "DECIMAL(20,5)")]
    [InlineData(5, 16 * 256 + 255, 0, "DECIMAL(16)")]
    [InlineData(6, 4, 0, "SERIAL")]
    [InlineData(7, 4, 0, "DATE")]
    [InlineData(8, 16 * 256 + 2, 0, "MONEY(16,2)")]
    [InlineData(10, 17 * 256 + 13, 0, "DATETIME YEAR TO FRACTION(3)")]
    [InlineData(10, 19 * 256 + 15, 0, "DATETIME YEAR TO FRACTION(5)")]
    [InlineData(10, 8 * 256 + 4, 0, "DATETIME YEAR TO DAY")]
    [InlineData(10, 6 * 256 + 106, 0, "DATETIME HOUR TO SECOND")]
    [InlineData(11, 56, 0, "BYTE")]
    [InlineData(12, 56, 0, "TEXT")]
    [InlineData(13, 40, 0, "VARCHAR(40)")]
    [InlineData(13, 10 * 256 + 40, 0, "VARCHAR(40,10)")]
    [InlineData(14, 7 * 256 + 2, 0, "INTERVAL YEAR(5) TO MONTH")]
    [InlineData(14, 9 * 256 + 74, 0, "INTERVAL DAY(3) TO SECOND")]
    [InlineData(14, 4 * 256 + 68, 0, "INTERVAL DAY(4) TO DAY")]
    [InlineData(15, 8, 0, "NCHAR(8)")]
    [InlineData(16, 30, 0, "NVARCHAR(30)")]
    [InlineData(17, 10, 0, "INT8")]
    [InlineData(18, 10, 0, "SERIAL8")]
    [InlineData(43, 200, 0, "LVARCHAR(200)")]
    [InlineData(40, 2000, 1, "LVARCHAR(2000)")]
    [InlineData(41, 1, 5, "BOOLEAN")]
    [InlineData(45, 1, 0, "BOOLEAN")]
    [InlineData(52, 8, 0, "BIGINT")]
    [InlineData(53, 8, 0, "BIGSERIAL")]
    [InlineData(41, 72, 10, "BLOB")]
    [InlineData(41, 72, 11, "CLOB")]
    [InlineData(41, 72, 99, "unknown(41)")]
    [InlineData(255, 0, 0, "unknown(255)")]
    [InlineData(2 + 256, 4, 0, "INTEGER")]
    public void Decode_GivesTheTypeText(int coltype, int collength, int extendedId, string expected) =>
        Assert.Equal(expected, InformixColumnType.Decode(coltype, collength, extendedId));

    [Fact]
    public void FlagBits_NotNullAndSerial()
    {
        Assert.True(InformixColumnType.IsNotNull(2 + 256));
        Assert.False(InformixColumnType.IsNotNull(2));
        Assert.True(InformixColumnType.IsSerial(6 + 256));
        Assert.True(InformixColumnType.IsSerial(53));
        Assert.False(InformixColumnType.IsSerial(2));
    }

    private static object?[] IndexRow(int tab, string name, string type, params int[] colnos) =>
        [tab, name, type, .. colnos.Select(c => (object?)c), .. Enumerable.Repeat<object?>(0, 16 - colnos.Length)];

    private static CannedConnection Catalogue() => new(
        ("tabid = 1", [["app"]]),
        ("from syscolumns",
        [
            [100, 1, "id", 6 + 256, 4, 0], [100, 2, "name", 13 + 256, 40, 0], [100, 3, "price", 5, (10 * 256) + 2, 0],
            [101, 1, "id", 2 + 256, 4, 0], [101, 2, "parent_id", 2, 4, 0], [101, 3, "note", 40, 2000, 1],
            [102, 1, "name", 13, 40, 0],
        ]),
        ("from sysconstraints", [[1, 100, "pk_parent", "P", "100_1"], [2, 101, "fk_child", "R", "101_2"], [3, 100, "u_name", "U", "100_3"]]),
        ("from sysindexes", [IndexRow(100, "100_1", "U", 1), IndexRow(100, "100_3", "U", 2), IndexRow(101, "101_2", "D", 2), IndexRow(101, "ix_child", "D", -3, 2)]),
        ("from sysreferences", [[2, 1, 100, "C"]]),
        ("from sysviews", [[102, "select name from "], [102, "parent                   "]]),
        ("from sysprocbody", [[200, "create function f(a int) returning int;\n"], [200, "return a; end function;"]]),
        ("from sysprocedures", [[200, "app", "f", "f", "integer"]]),
        ("datakey = 'D'", [[300, "create trigger t1 insert on child "]]),
        ("datakey = 'A'", [[300, "   after\n(update parent set name = name);"]]),
        ("from systriggers", [[300, "t1", 101, "I"]]),
        ("from systables", [[100, "app", "parent", "T"], [101, "app", "child", "T"], [102, "app", "v_parent", "V"]]));

    [Fact]
    public async Task Read_MapsTablesColumnsKeysIndexesViewsRoutinesAndTriggers()
    {
        var snapshot = await InformixSchemaReader.ReadAsync(Catalogue(), TestContext.Current.CancellationToken);

        Assert.Equal("app", snapshot.DefaultSchema);
        Assert.Equal(["child", "parent", "v_parent"], snapshot.Tables.Select(t => t.Name).Order());
        var parent = snapshot.FindTable("app", "parent")!;
        Assert.Equal("pk_parent", parent.PrimaryKeyName);
        Assert.Equal(
            [("id", "SERIAL", false, true, true), ("name", "VARCHAR(40)", false, false, false), ("price", "DECIMAL(10,2)", true, false, false)],
            parent.Columns.Select(c => (c.Name, c.DataType, c.IsNullable, c.IsPrimaryKey, c.IsAutoIncrement)));
        var unique = Assert.Single(parent.Indexes); // the primary key index is not listed
        Assert.Equal("u_name,True,name", $"{unique.Name},{unique.IsUnique},{string.Join('+', unique.Columns)}");

        var child = snapshot.FindTable("app", "child")!;
        Assert.Equal("LVARCHAR(2000)", child.Columns[2].DataType);
        var index = Assert.Single(child.Indexes); // the foreign-key index is not listed; sort order is dropped
        Assert.Equal("ix_child,False,note+parent_id", $"{index.Name},{index.IsUnique},{string.Join('+', index.Columns)}");
        var fk = Assert.Single(child.ForeignKeys);
        Assert.Equal(("fk_child", "app", "parent", ReferentialAction.Cascade), (fk.Name, fk.ReferencedSchema, fk.ReferencedTable, fk.OnDelete));
        Assert.Equal(["parent_id"], fk.Columns);
        Assert.Equal(["id"], fk.ReferencedColumns);

        var view = snapshot.FindTable("app", "v_parent")!;
        Assert.Equal(TableKind.View, view.Kind);
        Assert.Equal("select name from parent", view.Definition); // chunks joined unchanged, then trimmed

        var routine = Assert.Single(snapshot.Routines);
        Assert.Equal((RoutineKind.Function, "f(integer)", "int"), (routine.Kind, routine.Signature, routine.ReturnType));
        Assert.Equal("create function f(a int) returning int;\nreturn a; end function;", routine.Definition);
        var trigger = Assert.Single(snapshot.Triggers);
        Assert.Equal(("app", "child", "t1", "AFTER", "INSERT"), (trigger.Schema, trigger.Table, trigger.Name, trigger.Timing, trigger.Events));
        Assert.StartsWith("create trigger t1 insert on child\nafter", trigger.Definition);
    }
}

/// <summary>An <see cref="IDatabaseConnection"/> that answers each statement with the rows of the first rule whose text it contains.</summary>
internal sealed class CannedConnection(params (string Needle, object?[][] Rows)[] rules) : IDatabaseConnection
{
    public string ServerVersion => "canned";

    public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
    {
        var rows = rules.First(r => sql.Contains(r.Needle, StringComparison.Ordinal)).Rows;
        return ValueTask.FromResult<IResultReader>(new CannedReader(rows));
    }

    public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed class CannedReader(object?[][] rows) : IResultReader
    {
        private bool _done;

        public IReadOnlyList<ColumnInfo> Columns => [];

        public long RecordsAffected => -1;

        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken)
        {
            var batch = _done ? null : rows;
            _done = true;
            return ValueTask.FromResult(batch);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
