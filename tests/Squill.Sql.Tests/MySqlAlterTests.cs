using Squill.Core;
using Squill.Core.Design;
using Squill.Sql.Ddl;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class MySqlAlterTests
{
    private static ColumnDesign Col(string name, string type = "int", bool nullable = true, string? def = null) =>
        new(name, name, type, nullable, def, false);

    private static TableDesign Base(string? schema = "db", string[]? key = null) =>
        new(schema, "t", [Col("id", nullable: false), Col("a"), Col("b", "text")], key ?? ["id"], null, [], []);

    private static IReadOnlyList<string> Alter(TableDesign o, TableDesign e) => DdlGenerators.For(DatabaseDialect.MySql).Alter(o, e);

    private static TableDesign With(TableDesign t, ColumnDesign[]? columns = null, IndexDesign[]? indexes = null, ForeignKeyDesign[]? fks = null, string? name = null) =>
        t with { Columns = columns ?? t.Columns, Indexes = indexes ?? t.Indexes, ForeignKeys = fks ?? t.ForeignKeys, Name = name ?? t.Name };

    [Fact]
    public void Alter_RenameTable_KeepsSchemaOnBothSides() =>
        Assert.Equal(["RENAME TABLE `db`.`t` TO `db`.`u`"], Alter(Base(), With(Base(), name: "u")));

    [Fact]
    public void Alter_RenameTable_WithoutSchema() =>
        Assert.Equal(["RENAME TABLE `t` TO `u`"], Alter(Base(null), With(Base(null), name: "u")));

    [Fact]
    public void Alter_AddAndDropColumn_Statements()
    {
        var o = Base();
        var added = With(o, [.. o.Columns, new ColumnDesign(null, "c", "int", false, null, true)]);
        Assert.Equal(["ALTER TABLE `db`.`t` ADD COLUMN `c` int AUTO_INCREMENT NOT NULL"], Alter(o, added));
        Assert.Equal(["ALTER TABLE `db`.`t` DROP COLUMN `b`"], Alter(o, With(o, [o.Columns[0], o.Columns[1]])));
    }

    [Fact]
    public void Alter_RenameColumn_Statement()
    {
        var o = Base();
        var e = With(o, [o.Columns[0], o.Columns[1] with { Name = "z" }, o.Columns[2]]);
        Assert.Equal(["ALTER TABLE `db`.`t` RENAME COLUMN `a` TO `z`"], Alter(o, e));
    }

    [Fact]
    public void Alter_TypeNullabilityDefault_IsOneModifyStatement()
    {
        var o = Base();
        var e = With(o, [o.Columns[0], o.Columns[1] with { DataType = "bigint", IsNullable = false, DefaultExpression = "0" }, o.Columns[2]]);
        Assert.Equal(["ALTER TABLE `db`.`t` MODIFY COLUMN `a` bigint NOT NULL DEFAULT 0"], Alter(o, e));
    }

    [Fact]
    public void Alter_DefaultDroppedToNull_OmitsDefault()
    {
        var o = With(Base(), [Col("id", nullable: false), Col("a", def: "1")]);
        var e = With(o, [o.Columns[0], o.Columns[1] with { DefaultExpression = null }]);
        Assert.Equal(["ALTER TABLE `db`.`t` MODIFY COLUMN `a` int"], Alter(o, e));
    }

    [Fact]
    public void Alter_ModifyKeepsAutoIncrement()
    {
        var o = With(Base(), [new ColumnDesign("id", "id", "int", false, null, true)]);
        var e = With(o, [o.Columns[0] with { DataType = "bigint" }]);
        Assert.Equal(["ALTER TABLE `db`.`t` MODIFY COLUMN `id` bigint AUTO_INCREMENT NOT NULL"], Alter(o, e));
    }

    [Fact]
    public void Alter_PrimaryKeyChange_DropsThenAdds() =>
        Assert.Equal(
            ["ALTER TABLE `db`.`t` DROP PRIMARY KEY", "ALTER TABLE `db`.`t` ADD PRIMARY KEY (`id`, `a`)"],
            Alter(Base(), Base(key: ["id", "a"])));

    [Fact]
    public void Alter_Indexes_CreateAndDrop()
    {
        var o = With(Base(), indexes: [new IndexDesign("ix_a", "ix_a", false, ["a"])]);
        var e = With(o, indexes: [new IndexDesign(null, "ix_b", true, ["b"])]);
        Assert.Equal(["DROP INDEX `ix_a` ON `db`.`t`", "CREATE UNIQUE INDEX `ix_b` ON `db`.`t` (`b`)"], Alter(o, e));
    }

    [Fact]
    public void Alter_ForeignKeys_AddAndDrop()
    {
        var fk = new ForeignKeyDesign("fk_a", "fk_a", ["a"], "ref", "r", ["id"], ReferentialAction.Cascade, ReferentialAction.NoAction);
        var o = With(Base(), fks: [fk]);
        Assert.Equal(["ALTER TABLE `db`.`t` DROP FOREIGN KEY `fk_a`"], Alter(o, With(o, fks: [])));
        Assert.Equal(
            ["ALTER TABLE `db`.`t` ADD CONSTRAINT `fk_a` FOREIGN KEY (`a`) REFERENCES `ref`.`r` (`id`) ON DELETE CASCADE ON UPDATE NO ACTION"],
            Alter(Base(), With(Base(), fks: [fk with { OriginalName = null }])));
    }

    [Fact]
    public void Alter_MixedEdit_KeepsOperationOrder()
    {
        var o = With(Base(), indexes: [new IndexDesign("ix_a", "ix_a", false, ["a"])]);
        var e = With(o, [o.Columns[0], o.Columns[1] with { Name = "z" }, new ColumnDesign(null, "n", "text", true, null, false)], [], name: "u");
        Assert.Equal(
            [
                "DROP INDEX `ix_a` ON `db`.`t`",
                "ALTER TABLE `db`.`t` DROP COLUMN `b`",
                "ALTER TABLE `db`.`t` RENAME COLUMN `a` TO `z`",
                "ALTER TABLE `db`.`t` ADD COLUMN `n` text",
                "RENAME TABLE `db`.`t` TO `db`.`u`",
            ],
            Alter(o, e));
    }
}
