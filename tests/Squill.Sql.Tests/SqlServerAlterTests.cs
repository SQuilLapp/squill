using Squill.Core;
using Squill.Core.Design;
using Squill.Sql.Ddl;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class SqlServerAlterTests
{
    private static ColumnDesign Col(string name, string type = "int", bool nullable = true, string? def = null) =>
        new(name, name, type, nullable, def, false);

    private static TableDesign Base(string? schema = "dbo") =>
        new(schema, "t", [Col("id", nullable: false), Col("a"), Col("b", "nvarchar(10)")], ["id"], "pk_t", [], []);

    private static IReadOnlyList<string> Alter(TableDesign o, TableDesign e) => DdlGenerators.For(DatabaseDialect.SqlServer).Alter(o, e);

    private static TableDesign With(TableDesign t, ColumnDesign[]? columns = null, IndexDesign[]? indexes = null, ForeignKeyDesign[]? fks = null, string? name = null) =>
        t with { Columns = columns ?? t.Columns, Indexes = indexes ?? t.Indexes, ForeignKeys = fks ?? t.ForeignKeys, Name = name ?? t.Name };

    private static string DropBatch(string column) =>
        "DECLARE @n sysname = (SELECT dc.name FROM sys.default_constraints dc JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id "
        + $"WHERE dc.parent_object_id = OBJECT_ID(N'dbo.t') AND c.name = N'{column}'); "
        + "IF @n IS NOT NULL EXEC(N'ALTER TABLE [dbo].[t] DROP CONSTRAINT ' + QUOTENAME(@n))";

    [Fact]
    public void Alter_RenameTable_UsesSpRename() =>
        Assert.Equal(["EXEC sp_rename N'dbo.t', N'u'"], Alter(Base(), With(Base(), name: "u")));

    [Fact]
    public void Alter_RenameTable_WithoutSchema() =>
        Assert.Equal(["EXEC sp_rename N't', N'u'"], Alter(Base(null), With(Base(null), name: "u")));

    [Fact]
    public void Alter_AddColumn_StatesNullabilityAndIdentity()
    {
        var o = Base();
        var e = With(o, [.. o.Columns, new ColumnDesign(null, "c", "int", false, null, true), Col("d")]);
        Assert.Equal(
            ["ALTER TABLE [dbo].[t] ADD [c] int IDENTITY(1,1) NOT NULL", "ALTER TABLE [dbo].[t] ADD [d] int NULL"],
            Alter(o, e));
    }

    [Fact]
    public void Alter_DropColumn_WithoutDefault() =>
        Assert.Equal(["ALTER TABLE [dbo].[t] DROP COLUMN [b]"], Alter(Base(), With(Base(), [Base().Columns[0], Base().Columns[1]])));

    [Fact]
    public void Alter_DropColumn_WithDefault_DropsConstraintFirst()
    {
        var o = With(Base(), [Col("id", nullable: false), Col("a", def: "1")]);
        Assert.Equal([DropBatch("a"), "ALTER TABLE [dbo].[t] DROP COLUMN [a]"], Alter(o, With(o, [o.Columns[0]])));
    }

    [Fact]
    public void Alter_RenameColumn_UsesSpRename()
    {
        var o = Base();
        var e = With(o, [o.Columns[0], o.Columns[1] with { Name = "z" }, o.Columns[2]]);
        Assert.Equal(["EXEC sp_rename N'dbo.t.a', N'z', N'COLUMN'"], Alter(o, e));
    }

    [Fact]
    public void Alter_TypeAndNullability_IsOneAlterColumn()
    {
        var o = Base();
        var e = With(o, [o.Columns[0], o.Columns[1] with { DataType = "bigint", IsNullable = false }, o.Columns[2]]);
        Assert.Equal(["ALTER TABLE [dbo].[t] ALTER COLUMN [a] bigint NOT NULL"], Alter(o, e));
        var n = With(o, [o.Columns[0], o.Columns[1], o.Columns[2] with { IsNullable = false }]);
        Assert.Equal(["ALTER TABLE [dbo].[t] ALTER COLUMN [b] nvarchar(10) NOT NULL"], Alter(o, n));
    }

    [Fact]
    public void Alter_DefaultOnly_DropsThenAdds()
    {
        var o = With(Base(), [Col("id", nullable: false), Col("a", def: "1")]);
        Assert.Equal(
            [DropBatch("a"), "ALTER TABLE [dbo].[t] ADD DEFAULT 2 FOR [a]"],
            Alter(o, With(o, [o.Columns[0], o.Columns[1] with { DefaultExpression = "2" }])));
        Assert.Equal([DropBatch("a")], Alter(o, With(o, [o.Columns[0], o.Columns[1] with { DefaultExpression = null }])));
        var none = With(Base(), [Col("id", nullable: false), Col("a")]);
        Assert.Equal(
            ["ALTER TABLE [dbo].[t] ADD DEFAULT 5 FOR [a]"],
            Alter(none, With(none, [none.Columns[0], none.Columns[1] with { DefaultExpression = "5" }])));
    }

    [Fact]
    public void Alter_TypeChangeOnColumnWithDefault_IsThreeStatements()
    {
        var o = With(Base(), [Col("id", nullable: false), Col("a", def: "1")]);
        var e = With(o, [o.Columns[0], o.Columns[1] with { DataType = "bigint" }]);
        Assert.Equal(
            [DropBatch("a"), "ALTER TABLE [dbo].[t] ALTER COLUMN [a] bigint NULL", "ALTER TABLE [dbo].[t] ADD DEFAULT 1 FOR [a]"],
            Alter(o, e));
    }

    [Fact]
    public void Alter_PrimaryKeyChange_DropsByNameThenAdds() =>
        Assert.Equal(
            ["ALTER TABLE [dbo].[t] DROP CONSTRAINT [pk_t]", "ALTER TABLE [dbo].[t] ADD PRIMARY KEY ([id], [a])"],
            Alter(Base(), Base() with { PrimaryKey = ["id", "a"] }));

    [Fact]
    public void Alter_Indexes_CreateAndDrop()
    {
        var o = With(Base(), indexes: [new IndexDesign("ix_a", "ix_a", false, ["a"])]);
        var e = With(o, indexes: [new IndexDesign(null, "ix_b", true, ["b"])]);
        Assert.Equal(["DROP INDEX [ix_a] ON [dbo].[t]", "CREATE UNIQUE INDEX [ix_b] ON [dbo].[t] ([b])"], Alter(o, e));
    }

    [Fact]
    public void Alter_ForeignKeys_AddAndDrop_MapsRestrictToNoAction()
    {
        var fk = new ForeignKeyDesign("fk_a", "fk_a", ["a"], "dbo", "r", ["id"], ReferentialAction.Restrict, ReferentialAction.SetDefault);
        var o = With(Base(), fks: [fk]);
        Assert.Equal(["ALTER TABLE [dbo].[t] DROP CONSTRAINT [fk_a]"], Alter(o, With(o, fks: [])));
        Assert.Equal(
            ["ALTER TABLE [dbo].[t] ADD CONSTRAINT [fk_a] FOREIGN KEY ([a]) REFERENCES [dbo].[r] ([id]) ON DELETE NO ACTION ON UPDATE SET DEFAULT"],
            Alter(Base(), With(Base(), fks: [fk with { OriginalName = null }])));
    }

    [Fact]
    public void Alter_MixedEdit_KeepsOperationOrder()
    {
        var o = With(Base(), indexes: [new IndexDesign("ix_a", "ix_a", false, ["a"])]);
        var e = With(o, [o.Columns[0], o.Columns[1] with { Name = "z" }, new ColumnDesign(null, "n", "text", true, null, false)], [], name: "u");
        Assert.Equal(
            [
                "DROP INDEX [ix_a] ON [dbo].[t]",
                "ALTER TABLE [dbo].[t] DROP COLUMN [b]",
                "EXEC sp_rename N'dbo.t.a', N'z', N'COLUMN'",
                "ALTER TABLE [dbo].[t] ADD [n] text NULL",
                "EXEC sp_rename N'dbo.t', N'u'",
            ],
            Alter(o, e));
    }
}
