using Squill.Core.Design;
using Xunit;

namespace Squill.Core.Tests;

public sealed class TableDesignTests
{
    [Fact]
    public void FromTable_CopiesKeyAutoIncrementIndexAndForeignKey()
    {
        var table = new TableInfo(
            "public",
            "orders",
            TableKind.Table,
            [
                new ColumnDefinition("a", "int", false, true, null),
                new ColumnDefinition("id", "int", false, true, "nextval('s')", true),
                new ColumnDefinition("note", "text", true, false, "'x'"),
            ],
            [new IndexInfo("ix_note", true, ["note"])],
            [new ForeignKeyInfo("fk_a", ["a"], "public", "p", ["id"], ReferentialAction.Cascade, ReferentialAction.SetNull)],
            "pk_orders");

        var d = TableDesign.FromTable(table);

        Assert.Equal(["a", "id"], d.PrimaryKey);
        Assert.Equal("pk_orders", d.PrimaryKeyName);
        Assert.All(d.Columns, c => Assert.Equal(c.Name, c.OriginalName));
        Assert.Null(d.Columns[1].DefaultExpression);
        Assert.True(d.Columns[1].IsAutoIncrement);
        Assert.Equal("'x'", d.Columns[2].DefaultExpression);
        Assert.Equal(new IndexDesign("ix_note", "ix_note", true, ["note"]).Name, d.Indexes[0].Name);
        Assert.Equal("ix_note", d.Indexes[0].OriginalName);
        var fk = d.ForeignKeys[0];
        Assert.Equal("fk_a", fk.OriginalName);
        Assert.Equal(ReferentialAction.Cascade, fk.OnDelete);
        Assert.Equal(ReferentialAction.SetNull, fk.OnUpdate);
        Assert.Equal("p", fk.ReferencedTable);
    }

    [Fact]
    public void FromTable_View_Throws() =>
        Assert.Throws<ArgumentException>(() => TableDesign.FromTable(new TableInfo(null, "v", TableKind.View, [], [], [])));

    [Fact]
    public void New_IsEmpty()
    {
        var d = TableDesign.New("s", "t");
        Assert.Equal("s", d.Schema);
        Assert.Equal("t", d.Name);
        Assert.Empty(d.Columns);
        Assert.Empty(d.PrimaryKey);
        Assert.Null(d.PrimaryKeyName);
        Assert.Empty(d.Indexes);
        Assert.Empty(d.ForeignKeys);
    }
}
