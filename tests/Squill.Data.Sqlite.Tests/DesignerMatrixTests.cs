using Squill.Core;
using Squill.Core.Design;
using Squill.Sql.Ddl;
using Xunit;

namespace Squill.Data.Sqlite.Tests;

/// <summary>Applies every table designer operation to a populated SQLite table and checks the schema, the data and the dependent objects.</summary>
public sealed class DesignerMatrixTests
{
    private static readonly ConnectionProfile Memory =
        new(Guid.NewGuid(), "mem", DatabaseDialect.Sqlite, null, null, null, null, ":memory:", null);

    private static readonly ReferentialAction[] Actions =
        [ReferentialAction.NoAction, ReferentialAction.Restrict, ReferentialAction.Cascade, ReferentialAction.SetNull, ReferentialAction.SetDefault];

    /// <summary>One operation: its name, the edit and the column whose checksum must survive (by its name after the edit).</summary>
    public static TheoryData<string, string> Names()
    {
        var data = new TheoryData<string, string>();
        foreach (var name in Cases.Keys)
        {
            data.Add(name, Cases[name].Checksum);
        }

        return data;
    }

    private static Func<TableDesign, TableDesign> Edit(Func<TableDesign, TableDesign> f) => f;

    private static ColumnDesign Col(string name) => new(null, name, "TEXT", true, null, false);

    private static readonly Dictionary<string, (Func<TableDesign, TableDesign> Edit, string Checksum)> Cases = Build();

    private static Dictionary<string, (Func<TableDesign, TableDesign>, string)> Build()
    {
        var cases = new Dictionary<string, (Func<TableDesign, TableDesign>, string)>
        {
            ["add nullable column"] = (d => d with { Columns = [.. d.Columns, Col("n1")] }, "a"),
            ["add column with default"] = (d => d with { Columns = [.. d.Columns, new ColumnDesign(null, "n2", "TEXT", true, "'z'", false)] }, "a"),
            ["add not null column with default"] = (d => d with { Columns = [.. d.Columns, new ColumnDesign(null, "n3", "INTEGER", false, "7", false)] }, "a"),
            ["drop plain column"] = (d => d with { Columns = [.. d.Columns.Where(c => c.Name != "q")]}, "a"),
            ["drop indexed column"] = (d => d with { Columns = [.. d.Columns.Where(c => c.Name != "b")], Indexes = [.. d.Indexes.Where(i => i.Name != "ix_b")] }, "a"),
            ["drop column in a foreign key"] = (d => d with { Columns = [.. d.Columns.Where(c => c.Name != "p")], ForeignKeys = [] }, "a"),
            ["rename column"] = (d => d with { Columns = [.. d.Columns.Select(c => c.Name == "b" ? c with { Name = "b2" } : c)], Indexes = [d.Indexes[0] with { Columns = ["b2"] }, .. d.Indexes.Skip(1)] }, "id"),
            ["change type"] = (d => d with { Columns = [.. d.Columns.Select(c => c.Name == "b" ? c with { DataType = "BLOB" } : c)] }, "a"),
            ["make column not null"] = (d => d with { Columns = [.. d.Columns.Select(c => c.Name == "b" ? c with { IsNullable = false } : c)] }, "a"),
            ["make column nullable"] = (d => d with { Columns = [.. d.Columns.Select(c => c.Name == "c" ? c with { IsNullable = true } : c)] }, "a"),
            ["change default"] = (d => d with { Columns = [.. d.Columns.Select(c => c.Name == "c" ? c with { DefaultExpression = "'other'" } : c)] }, "a"),
            ["drop default"] = (d => d with { Columns = [.. d.Columns.Select(c => c.Name == "c" ? c with { DefaultExpression = null } : c)] }, "a"),
            ["composite primary key"] = (d => d with { PrimaryKey = ["id", "a"] }, "b"),
            ["drop primary key"] = (d => d with { PrimaryKey = [] }, "a"),
            ["add unique index"] = (d => d with { Indexes = [.. d.Indexes, new IndexDesign(null, "ux_a", true, ["a"])] }, "a"),
            ["drop unique index"] = (d => d with { Indexes = [.. d.Indexes.Where(i => i.Name != "ux_id")] }, "a"),
            ["drop foreign key"] = (d => d with { ForeignKeys = [] }, "a"),
            ["rename table"] = (d => d with { Name = "t2" }, "a"),
            ["rename table and change type"] = (d => d with { Name = "t2", Columns = [.. d.Columns.Select(c => c.Name == "b" ? c with { DataType = "BLOB" } : c)] }, "a"),
        };

        foreach (var action in Actions)
        {
            cases[$"add foreign key {action}"] = (d => d with { ForeignKeys = [.. d.ForeignKeys, new ForeignKeyDesign(null, "fk_q", ["q"], null, "parent", ["id"], action, action)] }, "a");
        }

        return cases;
    }

    private static async Task<List<object?[]>> RunAsync(IDatabaseConnection connection, string sql)
    {
        await using var reader = await connection.ExecuteAsync(sql, [], CancellationToken.None);
        return await reader.ReadAllAsync(1000, CancellationToken.None);
    }

    private static async Task<IDatabaseConnection> SeedAsync()
    {
        var connection = await new SqliteDatabaseProvider().OpenAsync(Memory, null, CancellationToken.None);
        var script = new List<string>
        {
            "PRAGMA foreign_keys = ON",
            "CREATE TABLE parent (id INTEGER PRIMARY KEY)",
            "INSERT INTO parent VALUES (1), (2)",
            "CREATE TABLE log (id INTEGER)",
            "CREATE TABLE t (id INTEGER NOT NULL, a INTEGER, b TEXT, c TEXT NOT NULL DEFAULT 'd', p INTEGER, q INTEGER, "
                + "PRIMARY KEY (id), CONSTRAINT fk_p FOREIGN KEY (p) REFERENCES parent (id) ON DELETE CASCADE)",
            "CREATE INDEX ix_b ON t (b)",
            "CREATE UNIQUE INDEX ux_id ON t (id)",
            "CREATE TRIGGER trg_t AFTER INSERT ON t BEGIN INSERT INTO log VALUES (NEW.id); END",
            "CREATE TABLE child (id INTEGER PRIMARY KEY, t_id INTEGER, FOREIGN KEY (t_id) REFERENCES t (id))",
            "INSERT INTO t (id, a, b, p, q) SELECT value, value * 3, 'v' || value, value % 2 + 1, value % 2 + 1 FROM (WITH RECURSIVE s(value) AS (SELECT 1 UNION ALL SELECT value + 1 FROM s WHERE value < 100) SELECT value FROM s)",
            "INSERT INTO child VALUES (1, 5), (2, 6)",
            "DELETE FROM log",
        };
        foreach (var sql in script)
        {
            await RunAsync(connection, sql);
        }

        return connection;
    }

    private static async Task<long> ChecksumAsync(IDatabaseConnection connection, string table, string column) =>
        (long)(await RunAsync(connection, $"SELECT sum(length(CAST(\"{column}\" AS TEXT))) + sum(id) + count(*) FROM \"{table}\""))[0][0]!;

    [Theory]
    [MemberData(nameof(Names))]
    public async Task Operation_ProducesSqlSqliteAcceptsAndKeepsData(string name, string checksumColumn)
    {
        await using var connection = await SeedAsync();
        var original = TableDesign.FromTable(
            (await connection.ReadSchemaAsync(CancellationToken.None)).FindTable(null, "t")!,
            (await connection.ReadSchemaAsync(CancellationToken.None)).Triggers);
        var edited = Cases[name].Edit(original);
        var before = await ChecksumAsync(connection, "t", checksumColumn);

        foreach (var sql in DdlGenerators.For(DatabaseDialect.Sqlite).Alter(original, edited))
        {
            var rows = await RunAsync(connection, sql);
            if (sql == "PRAGMA foreign_key_check")
            {
                Assert.Empty(rows);
            }
        }

        var schema = await connection.ReadSchemaAsync(CancellationToken.None);
        var table = schema.FindTable(null, edited.Name)!;
        Assert.Equal(edited.Columns.Select(c => c.Name), table.Columns.Select(c => c.Name));
        Assert.Equal(edited.Columns.Select(c => c.DataType.ToUpperInvariant()), table.Columns.Select(c => c.DataType.ToUpperInvariant()));
        Assert.Equal(edited.Columns.Select(c => c.IsNullable), table.Columns.Select(c => c.IsNullable));
        Assert.Equal(edited.Columns.Select(c => c.DefaultExpression), table.Columns.Select(c => c.DefaultExpression));
        Assert.Equal(edited.PrimaryKey, table.Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name));
        Assert.Equal(edited.Indexes.Select(i => i.Name).Order(), table.Indexes.Select(i => i.Name).Order());
        Assert.Equal(
            edited.ForeignKeys.Select(f => (string.Join(",", f.Columns), f.ReferencedTable, f.OnDelete, f.OnUpdate)).Order(),
            table.ForeignKeys.Select(f => (string.Join(",", f.Columns), f.ReferencedTable, f.OnDelete, f.OnUpdate)).Order());

        Assert.Equal(before, await ChecksumAsync(connection, edited.Name, checksumColumn));
        Assert.Empty(await RunAsync(connection, "PRAGMA foreign_key_check"));
        Assert.Equal("ok", (await RunAsync(connection, "PRAGMA integrity_check"))[0][0]);

        // The trigger survives and still fires, and the child's foreign key follows the table.
        var trigger = Assert.Single(schema.Triggers, x => x.Name == "trg_t");
        Assert.Equal(edited.Name, trigger.Table);
        await RunAsync(connection, "PRAGMA foreign_keys = OFF");
        var names = string.Join(", ", table.Columns.Select(c => $"\"{c.Name}\""));
        await RunAsync(connection, $"INSERT INTO \"{edited.Name}\" ({names}) VALUES ({string.Join(", ", table.Columns.Select(_ => "1000"))})");
        Assert.Equal(1L, (long)(await RunAsync(connection, "SELECT count(*) FROM log"))[0][0]!);
        Assert.Equal(edited.Name, Assert.Single(schema.FindTable(null, "child")!.ForeignKeys).ReferencedTable);
    }

    [Fact]
    public async Task DropTable_RemovesTableIndexAndTrigger()
    {
        await using var connection = await SeedAsync();
        await RunAsync(connection, "PRAGMA foreign_keys = OFF");
        foreach (var sql in DdlGenerators.For(DatabaseDialect.Sqlite).DropTable(null, "t"))
        {
            await RunAsync(connection, sql);
        }

        var schema = await connection.ReadSchemaAsync(CancellationToken.None);
        Assert.Null(schema.FindTable(null, "t"));
        Assert.DoesNotContain(schema.Triggers, x => x.Table == "t");
    }

    [Fact]
    public async Task CatalogTypes_RoundTripThroughTheReaderToTheSameEntry()
    {
        await using var connection = await new SqliteDatabaseProvider().OpenAsync(Memory, null, CancellationToken.None);
        var types = DataTypeCatalog.For(DatabaseDialect.Sqlite);
        var columns = types.Select((t, i) => new ColumnDesign(null, $"c{i}", t, true, null, false)).ToList();
        var design = new TableDesign(null, "types", columns, [], null, [], []);
        foreach (var sql in DdlGenerators.For(DatabaseDialect.Sqlite).CreateTable(design))
        {
            await RunAsync(connection, sql);
        }

        var table = (await connection.ReadSchemaAsync(CancellationToken.None)).FindTable(null, "types")!;
        Assert.Equal(types, table.Columns.Select(c => types.Single(t => string.Equals(t, c.DataType, StringComparison.OrdinalIgnoreCase))));
    }
}
