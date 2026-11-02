using Xunit;
using Squill.Core;

namespace Squill.Data.Sqlite.Tests;

public sealed class SqliteSchemaReaderTests
{
    private static readonly ConnectionProfile Memory =
        new(Guid.NewGuid(), "mem", DatabaseDialect.Sqlite, null, null, null, null, ":memory:", null);

    private static async Task<SchemaSnapshot> ReadAsync(params string[] statements)
    {
        await using var connection = await new SqliteDatabaseProvider().OpenAsync(Memory, null, CancellationToken.None);
        foreach (var sql in statements)
        {
            await using var reader = await connection.ExecuteAsync(sql, [], CancellationToken.None);
            await reader.ReadAllAsync(10, CancellationToken.None);
        }
        return await connection.ReadSchemaAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ReadSchemaAsync_EmptyDatabase_HasMainAndNoTables()
    {
        var schema = await ReadAsync();
        Assert.Equal("main", schema.DefaultSchema);
        Assert.Empty(schema.Tables);
    }

    [Fact]
    public async Task ReadSchemaAsync_TablesViewsAndColumns_AreDescribed()
    {
        var schema = await ReadAsync(
            "CREATE TABLE person (id INTEGER PRIMARY KEY, name TEXT NOT NULL DEFAULT 'x', a INTEGER, b INTEGER, total INTEGER GENERATED ALWAYS AS (a + b) VIRTUAL)",
            "CREATE VIEW v AS SELECT id FROM person");

        Assert.Equal(["person", "v"], schema.Tables.Select(t => t.Name));
        Assert.All(schema.Tables, t => Assert.Equal("main", t.Schema));
        var person = schema.FindTable(null, "person")!;
        Assert.Equal(TableKind.Table, person.Kind);
        Assert.Equal(["id", "name", "a", "b"], person.Columns.Select(c => c.Name));
        Assert.Equal(new ColumnDefinition("id", "INTEGER", true, true, null, true), person.Columns[0]);
        Assert.Equal(new ColumnDefinition("name", "TEXT", false, false, "'x'"), person.Columns[1]);
        Assert.Equal(TableKind.View, schema.FindTable(null, "v")!.Kind);
        Assert.Equal(["id"], schema.FindTable(null, "v")!.Columns.Select(c => c.Name));
    }

    [Fact]
    public async Task ReadSchemaAsync_Indexes_SkipPrimaryKeyAutoIndex()
    {
        var schema = await ReadAsync(
            "CREATE TABLE t (id TEXT PRIMARY KEY, a INTEGER, b INTEGER, c TEXT) WITHOUT ROWID",
            "CREATE INDEX ix_ab ON t (a, b)",
            "CREATE UNIQUE INDEX ux_c ON t (c)");

        var indexes = schema.FindTable(null, "t")!.Indexes.OrderBy(i => i.Name).ToList();
        Assert.Equal(["ix_ab", "ux_c"], indexes.Select(i => i.Name));
        Assert.Equal(["a", "b"], indexes[0].Columns);
        Assert.False(indexes[0].IsUnique);
        Assert.Equal(["c"], indexes[1].Columns);
        Assert.True(indexes[1].IsUnique);
    }

    [Fact]
    public async Task ReadSchemaAsync_ForeignKeys_AreGroupedAndResolved()
    {
        var schema = await ReadAsync(
            "CREATE TABLE parent (a INTEGER, b INTEGER, PRIMARY KEY (a, b))",
            "CREATE TABLE child (x INTEGER, y INTEGER, z INTEGER REFERENCES parent, FOREIGN KEY (x, y) REFERENCES parent (a, b))");

        var fks = schema.FindTable(null, "child")!.ForeignKeys;
        Assert.Equal(2, fks.Count);
        var composite = Assert.Single(fks, f => f.Columns.Count == 2);
        Assert.Equal(["x", "y"], composite.Columns);
        Assert.Equal("parent", composite.ReferencedTable);
        Assert.Equal("main", composite.ReferencedSchema);
        Assert.Equal(["a", "b"], composite.ReferencedColumns);
        Assert.Equal(["a", "b"], Assert.Single(fks, f => f.Columns[0] == "z").ReferencedColumns);
    }

    [Fact]
    public async Task ReadSchemaAsync_InternalTablesAndQuotedNames_AreHandled()
    {
        var schema = await ReadAsync(
            "CREATE TABLE \"we\"\"ird\" (id INTEGER PRIMARY KEY AUTOINCREMENT, \"my col\" TEXT)");

        var table = Assert.Single(schema.Tables);
        Assert.Equal("we\"ird", table.Name);
        Assert.Equal(["id", "my col"], table.Columns.Select(c => c.Name));
    }

    [Fact]
    public async Task ReadSchemaAsync_AutoIncrement_OnlyForSingleIntegerPrimaryKey()
    {
        var schema = await ReadAsync(
            "CREATE TABLE a (id INTEGER PRIMARY KEY, n INTEGER)",
            "CREATE TABLE b (id INT PRIMARY KEY)",
            "CREATE TABLE c (x INTEGER, y INTEGER, PRIMARY KEY (x, y))");

        Assert.True(schema.FindTable(null, "a")!.Columns[0].IsAutoIncrement);
        Assert.False(schema.FindTable(null, "a")!.Columns[1].IsAutoIncrement);
        Assert.False(schema.FindTable(null, "b")!.Columns[0].IsAutoIncrement);
        Assert.All(schema.FindTable(null, "c")!.Columns, c => Assert.False(c.IsAutoIncrement));
    }

    [Fact]
    public async Task ReadSchemaAsync_ForeignKeyActions_AreReadBack()
    {
        var schema = await ReadAsync(
            "CREATE TABLE p (id INTEGER PRIMARY KEY)",
            "CREATE TABLE c (a INTEGER REFERENCES p ON DELETE CASCADE ON UPDATE SET NULL, b INTEGER REFERENCES p)");

        var fks = schema.FindTable(null, "c")!.ForeignKeys;
        var withActions = Assert.Single(fks, f => f.Columns[0] == "a");
        Assert.Equal(ReferentialAction.Cascade, withActions.OnDelete);
        Assert.Equal(ReferentialAction.SetNull, withActions.OnUpdate);
        var plain = Assert.Single(fks, f => f.Columns[0] == "b");
        Assert.Equal(ReferentialAction.NoAction, plain.OnDelete);
        Assert.Equal(ReferentialAction.NoAction, plain.OnUpdate);
    }
}
