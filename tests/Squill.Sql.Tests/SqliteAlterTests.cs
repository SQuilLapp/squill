using Squill.Core;
using Squill.Core.Design;
using Squill.Sql.Ddl;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class SqliteAlterTests
{
    private static ColumnDesign Col(string name, string type = "INTEGER", bool nullable = true, string? def = null) =>
        new(name, name, type, nullable, def, false);

    private static TableDesign Base() =>
        new(null, "t", [Col("id", nullable: false), Col("a"), Col("b", "TEXT")], ["id"], null, [], []);

    private static IReadOnlyList<string> Alter(TableDesign o, TableDesign e) => DdlGenerators.For(DatabaseDialect.Sqlite).Alter(o, e);

    [Fact]
    public void Alter_NoChange_ReturnsEmpty() => Assert.Empty(Alter(Base(), Base()));

    [Fact]
    public void Alter_NativeOperations_UseAlterTable()
    {
        var o = Base() with { Indexes = [new IndexDesign("ix_a", "ix_a", false, ["a"])] };
        var e = o with
        {
            Name = "u",
            Columns = [o.Columns[0], o.Columns[1] with { Name = "a2" }, new ColumnDesign(null, "c", "TEXT", true, null, false)],
            Indexes = [new IndexDesign(null, "ux_c", true, ["c"])],
        };
        Assert.Equal(
            [
                "DROP INDEX \"ix_a\"",
                "ALTER TABLE \"t\" DROP COLUMN \"b\"",
                "ALTER TABLE \"t\" RENAME COLUMN \"a\" TO \"a2\"",
                "ALTER TABLE \"t\" ADD COLUMN \"c\" TEXT",
                "CREATE UNIQUE INDEX \"ux_c\" ON \"t\" (\"c\")",
                "ALTER TABLE \"t\" RENAME TO \"u\"",
            ],
            Alter(o, e));
    }

    [Fact]
    public void Alter_AddNotNullColumnWithoutDefault_Rebuilds()
    {
        var o = Base();
        var e = o with { Columns = [.. o.Columns, new ColumnDesign(null, "c", "TEXT", false, null, false)] };
        Assert.Equal("PRAGMA foreign_keys = OFF", Alter(o, e)[0]);
    }

    [Fact]
    public void Alter_AddNotNullColumnWithDefault_IsNative() =>
        Assert.Equal(
            ["ALTER TABLE \"t\" ADD COLUMN \"c\" TEXT NOT NULL DEFAULT 'x'"],
            Alter(Base(), Base() with { Columns = [.. Base().Columns, new ColumnDesign(null, "c", "TEXT", false, "'x'", false)] }));

    [Fact]
    public void Alter_TypeChange_RebuildsCopyingValues()
    {
        var o = Base();
        var e = o with { Columns = [o.Columns[0], o.Columns[1] with { DataType = "TEXT" }, o.Columns[2]] };
        Assert.Equal(
            [
                "PRAGMA foreign_keys = OFF",
                "CREATE TABLE \"t__squill_new\" (\n    \"id\" INTEGER NOT NULL,\n    \"a\" TEXT,\n    \"b\" TEXT,\n    PRIMARY KEY (\"id\")\n)",
                "INSERT INTO \"t__squill_new\" (\"id\", \"a\", \"b\") SELECT \"id\", \"a\", \"b\" FROM \"t\"",
                "DROP TABLE \"t\"",
                "ALTER TABLE \"t__squill_new\" RENAME TO \"t\"",
                "PRAGMA foreign_key_check",
                "PRAGMA foreign_keys = ON",
            ],
            Alter(o, e));
    }

    [Fact]
    public void Alter_PrimaryKeyChange_RebuildsWithRenamesAndIndexes()
    {
        var o = Base() with { Indexes = [new IndexDesign("ix_b", "ix_b", false, ["b"])] };
        var e = o with
        {
            Name = "u",
            Columns = [o.Columns[0], o.Columns[1] with { Name = "a2", DataType = "TEXT" }, o.Columns[2], new ColumnDesign(null, "n", "TEXT", true, null, false)],
            PrimaryKey = ["id", "a2"],
        };
        Assert.Equal(
            [
                "PRAGMA foreign_keys = OFF",
                "CREATE TABLE \"u__squill_new\" (\n    \"id\" INTEGER NOT NULL,\n    \"a2\" TEXT,\n    \"b\" TEXT,\n    \"n\" TEXT,\n    PRIMARY KEY (\"id\", \"a2\")\n)",
                "INSERT INTO \"u__squill_new\" (\"id\", \"a2\", \"b\") SELECT \"id\", \"a\", \"b\" FROM \"t\"",
                "DROP TABLE \"t\"",
                "ALTER TABLE \"u__squill_new\" RENAME TO \"u\"",
                "CREATE INDEX \"ix_b\" ON \"u\" (\"b\")",
                "PRAGMA foreign_key_check",
                "PRAGMA foreign_keys = ON",
            ],
            Alter(o, e));
    }

    [Fact]
    public void Alter_ForeignKeyAdded_RebuildsWithConstraint()
    {
        var o = Base();
        var e = o with { ForeignKeys = [new ForeignKeyDesign(null, "fk_a", ["a"], null, "p", ["id"], ReferentialAction.Cascade, ReferentialAction.NoAction)] };
        var sql = Alter(o, e);
        Assert.Contains("CONSTRAINT \"fk_a\" FOREIGN KEY (\"a\") REFERENCES \"p\" (\"id\") ON DELETE CASCADE ON UPDATE NO ACTION", sql[1]);
        Assert.Equal(7, sql.Count);
    }

    [Fact]
    public void Alter_DropIndexedColumn_Rebuilds()
    {
        var o = Base() with { Indexes = [new IndexDesign("ix_a", "ix_a", false, ["a"])] };
        var e = o with { Columns = [o.Columns[0], o.Columns[2]], Indexes = [] };
        var sql = Alter(o, e);
        Assert.Equal("INSERT INTO \"t__squill_new\" (\"id\", \"b\") SELECT \"id\", \"b\" FROM \"t\"", sql[2]);
        Assert.DoesNotContain(sql, s => s.Contains("DROP COLUMN", StringComparison.Ordinal));
    }

    [Fact]
    public void Alter_MixedNativeAndNonNative_RebuildsOnce()
    {
        var o = Base();
        var e = o with
        {
            Name = "u",
            Columns = [o.Columns[0], o.Columns[1] with { Name = "a2", DataType = "TEXT" }, o.Columns[2]],
        };
        var sql = Alter(o, e);
        Assert.Single(sql, s => s == "PRAGMA foreign_keys = OFF");
        Assert.Single(sql, s => s.StartsWith("DROP TABLE", StringComparison.Ordinal));
        Assert.DoesNotContain(sql, s => s.StartsWith("ALTER TABLE \"t\" RENAME", StringComparison.Ordinal));
    }
}
