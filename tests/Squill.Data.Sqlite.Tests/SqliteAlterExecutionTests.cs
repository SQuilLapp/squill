using Squill.Core;
using Squill.Core.Design;
using Squill.Sql.Ddl;
using Xunit;

namespace Squill.Data.Sqlite.Tests;

public sealed class SqliteAlterExecutionTests
{
    private static readonly ConnectionProfile Memory =
        new(Guid.NewGuid(), "mem", DatabaseDialect.Sqlite, null, null, null, null, ":memory:", null);

    private static async Task<List<object?[]>> RunAsync(IDatabaseConnection connection, string sql)
    {
        await using var reader = await connection.ExecuteAsync(sql, [], CancellationToken.None);
        return await reader.ReadAllAsync(100, CancellationToken.None);
    }

    // Seeds the original table plus a parent, applies the edit and returns the table read back and its rows.
    private static async Task<(TableInfo Table, List<object?[]> Rows)> ApplyAsync(Func<TableDesign, TableDesign> edit, string select)
    {
        await using var connection = await new SqliteDatabaseProvider().OpenAsync(Memory, null, CancellationToken.None);
        foreach (var sql in new[]
        {
            "CREATE TABLE parent (id INTEGER PRIMARY KEY)",
            "INSERT INTO parent VALUES (1), (2)",
            "CREATE TABLE t (id INTEGER NOT NULL, a INTEGER, b TEXT, PRIMARY KEY (id))",
            "CREATE INDEX ix_b ON t (b)",
            "INSERT INTO t VALUES (1, 10, 'x'), (2, 20, 'y')",
        })
        {
            await RunAsync(connection, sql);
        }

        var original = TableDesign.FromTable((await connection.ReadSchemaAsync(CancellationToken.None)).FindTable(null, "t")!);
        var edited = edit(original);
        var statements = DdlGenerators.For(DatabaseDialect.Sqlite).Alter(original, edited);
        foreach (var sql in statements)
        {
            var rows = await RunAsync(connection, sql);
            if (sql == "PRAGMA foreign_key_check")
            {
                Assert.Empty(rows);
            }
        }

        var table = (await connection.ReadSchemaAsync(CancellationToken.None)).FindTable(null, edited.Name)!;
        return (table, await RunAsync(connection, select));
    }

    [Fact]
    public async Task Native_RenameColumnAddColumnAndRenameTable_KeepRows()
    {
        var (table, rows) = await ApplyAsync(
            d => d with
            {
                Name = "u",
                Columns = [d.Columns[0], d.Columns[1] with { Name = "a2" }, d.Columns[2], new ColumnDesign(null, "c", "TEXT", true, null, false)],
            },
            "SELECT id, a2, b, c FROM u ORDER BY id");
        Assert.Equal(["id", "a2", "b", "c"], table.Columns.Select(c => c.Name));
        Assert.Equal([1L, 10L, "x", null], rows[0]);
        Assert.Equal([2L, 20L, "y", null], rows[1]);
    }

    [Fact]
    public async Task Rebuild_TypeChangeRenameAndPrimaryKeyChange_KeepRows()
    {
        var (table, rows) = await ApplyAsync(
            d => d with
            {
                Columns = [d.Columns[0], d.Columns[1] with { Name = "a2", DataType = "TEXT" }, d.Columns[2]],
                PrimaryKey = ["id", "a2"],
            },
            "SELECT id, a2, b FROM t ORDER BY id");
        Assert.Equal(["id", "a2", "b"], table.Columns.Select(c => c.Name));
        Assert.Equal("TEXT", table.Columns[1].DataType);
        Assert.Equal(["id", "a2"], table.Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name));
        Assert.Equal(["ix_b"], table.Indexes.Select(i => i.Name));
        Assert.Equal([1L, "10", "x"], rows[0]);
    }

    [Fact]
    public async Task Rebuild_ForeignKeyAdded_PassesForeignKeyCheck()
    {
        var (table, rows) = await ApplyAsync(
            d => d with { ForeignKeys = [new ForeignKeyDesign(null, "fk_p", ["id"], null, "parent", ["id"], ReferentialAction.Cascade, ReferentialAction.NoAction)] },
            "SELECT COUNT(*) FROM t");
        var fk = Assert.Single(table.ForeignKeys);
        Assert.Equal("parent", fk.ReferencedTable);
        Assert.Equal(2L, rows[0][0]);
    }

    [Fact]
    public async Task Rebuild_DropIndexedColumn_DropsItsIndexAndKeepsOtherColumns()
    {
        var (table, rows) = await ApplyAsync(
            d => d with { Columns = [d.Columns[0], d.Columns[1]], Indexes = [] },
            "SELECT id, a FROM t ORDER BY id");
        Assert.Equal(["id", "a"], table.Columns.Select(c => c.Name));
        Assert.Empty(table.Indexes);
        Assert.Equal([2L, 20L], rows[1]);
    }
}
