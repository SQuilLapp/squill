using Squill.Core;
using Squill.Core.Design;
using Squill.Sql.Ddl;
using Xunit;

namespace Squill.Data.Sqlite.Tests;

public sealed class SqliteDdlRoundTripTests
{
    private static readonly ConnectionProfile Memory =
        new(Guid.NewGuid(), "mem", DatabaseDialect.Sqlite, null, null, null, null, ":memory:", null);

    [Fact]
    public async Task CreateTable_GeneratedStatements_ReadBackAsDesigned()
    {
        var generator = DdlGenerators.For(DatabaseDialect.Sqlite);
        var parent = new TableDesign(
            null, "parent",
            [new ColumnDesign(null, "id", "INTEGER", false, null, true), new ColumnDesign(null, "label", "TEXT", false, "'x'", false)],
            ["id"], null, [], []);
        var child = new TableDesign(
            null, "child",
            [new ColumnDesign(null, "id", "INTEGER", false, null, false), new ColumnDesign(null, "parent_id", "INTEGER", true, null, false), new ColumnDesign(null, "code", "TEXT", true, null, false)],
            ["id"], null,
            [new IndexDesign(null, "ux_code", true, ["code"])],
            [new ForeignKeyDesign(null, "fk_parent", ["parent_id"], null, "parent", ["id"], ReferentialAction.Cascade, ReferentialAction.SetNull)]);

        await using var connection = await new SqliteDatabaseProvider().OpenAsync(Memory, null, CancellationToken.None);
        foreach (var sql in generator.CreateTable(parent).Concat(generator.CreateTable(child)))
        {
            await using var reader = await connection.ExecuteAsync(sql, [], CancellationToken.None);
            await reader.ReadAllAsync(10, CancellationToken.None);
        }

        var schema = await connection.ReadSchemaAsync(CancellationToken.None);
        var p = schema.FindTable(null, "parent")!;
        Assert.Equal(new ColumnDefinition("id", "INTEGER", true, true, null, true), p.Columns[0]);
        Assert.Equal(new ColumnDefinition("label", "TEXT", false, false, "'x'"), p.Columns[1]);

        var c = schema.FindTable(null, "child")!;
        Assert.Equal(["id", "parent_id", "code"], c.Columns.Select(x => x.Name));
        Assert.Equal([false, true, true], c.Columns.Select(x => x.IsNullable));
        Assert.Equal(["id"], c.Columns.Where(x => x.IsPrimaryKey).Select(x => x.Name));
        var index = Assert.Single(c.Indexes);
        Assert.Equal(("ux_code", true), (index.Name, index.IsUnique));
        Assert.Equal(["code"], index.Columns);
        var fk = Assert.Single(c.ForeignKeys);
        Assert.Equal(["parent_id"], fk.Columns);
        Assert.Equal(("parent", ReferentialAction.Cascade, ReferentialAction.SetNull), (fk.ReferencedTable, fk.OnDelete, fk.OnUpdate));
    }
}
