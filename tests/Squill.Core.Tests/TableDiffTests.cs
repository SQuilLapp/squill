using Squill.Core.Design;
using Xunit;

namespace Squill.Core.Tests;

public sealed class TableDiffTests
{
    private static TableDesign Original() => TableDesign.FromTable(new TableInfo(
        "public",
        "orders",
        TableKind.Table,
        [
            new ColumnDefinition("id", "int", false, true, null),
            new ColumnDefinition("a", "int", false, false, null),
            new ColumnDefinition("note", "text", true, false, "'x'"),
        ],
        [new IndexInfo("ix_note", false, ["note"]), new IndexInfo("ix_a", false, ["a"])],
        [new ForeignKeyInfo("fk_a", ["a"], "public", "p", ["id"], ReferentialAction.NoAction, ReferentialAction.NoAction)],
        "pk_orders"));

    private static IReadOnlyList<SchemaOperation> Diff(Func<TableDesign, TableDesign> edit) => TableDiff.Compute(Original(), edit(Original()));

    private static TableDesign Col(TableDesign d, string name, Func<ColumnDesign, ColumnDesign> f) =>
        d with { Columns = [.. d.Columns.Select(c => c.Name == name ? f(c) : c)] };

    [Fact]
    public void Identical_IsEmpty() => Assert.Empty(Diff(d => d));

    [Fact]
    public void RenameTable_Alone() =>
        Assert.Equal([new RenameTable("orders", "o2")], Diff(d => d with { Name = "o2" }));

    [Fact]
    public void AddColumn_Alone()
    {
        var col = new ColumnDesign(null, "n", "int", true, null, false);
        Assert.Equal([new AddColumn(col)], Diff(d => d with { Columns = [.. d.Columns, col] }));
    }

    [Fact]
    public void DropColumn_Alone() =>
        Assert.Equal([new DropIndex("ix_note"), new DropColumn("note")], Diff(d => d with { Columns = [.. d.Columns.Where(c => c.Name != "note")], Indexes = [d.Indexes[1]] }));

    [Fact]
    public void RenameColumn_Alone()
    {
        var ops = Diff(d => Col(d, "note", c => c with { Name = "memo" }));
        Assert.Equal([new RenameColumn("note", "memo")], ops);
    }

    [Fact]
    public void RenameAndTypeChange_OneColumn()
    {
        var ops = Diff(d => Col(d, "note", c => c with { Name = "memo", DataType = "TEXT2" }));
        var alter = Assert.IsType<AlterColumn>(ops[1]);
        Assert.Equal(new RenameColumn("note", "memo"), ops[0]);
        Assert.Equal("memo", alter.Before.Name);
        Assert.Equal("memo", alter.After.Name);
        Assert.Equal(ColumnChange.Type, alter.Changes);
    }

    [Fact]
    public void AlterColumn_FlagsAndNormalization()
    {
        var ops = Diff(d => Col(Col(d, "note", c => c with { IsNullable = false, DefaultExpression = null }), "a", c => c with { DataType = " INT ", DefaultExpression = "  " }));
        var alter = Assert.IsType<AlterColumn>(Assert.Single(ops));
        Assert.Equal(ColumnChange.Nullability | ColumnChange.Default, alter.Changes);
    }

    [Fact]
    public void AutoIncrementChange_GivesNothing() =>
        Assert.Empty(Diff(d => Col(d, "a", c => c with { IsAutoIncrement = true })));

    [Fact]
    public void CompositeKeyChange_DropsAndAdds()
    {
        var ops = Diff(d => d with { PrimaryKey = ["id", "a"] });
        Assert.Equal(new DropPrimaryKey("pk_orders"), ops[0]);
        Assert.Equal(["id", "a"], Assert.IsType<AddPrimaryKey>(ops[1]).Columns);
        Assert.Equal(2, ops.Count);
    }

    [Fact]
    public void KeyOnRenamedColumn_IsUnchanged() =>
        Assert.Equal([new RenameColumn("id", "oid")], Diff(d => Col(d, "id", c => c with { Name = "oid" }) with { PrimaryKey = ["oid"] }));

    [Fact]
    public void Index_RecreatedWhenUniquenessFlips()
    {
        var ops = Diff(d => d with { Indexes = [d.Indexes[0] with { IsUnique = true }, d.Indexes[1]] });
        Assert.Equal(new DropIndex("ix_note"), ops[0]);
        var ix = Assert.IsType<CreateIndex>(ops[1]).Index;
        Assert.True(ix.IsUnique);
        Assert.Equal(["note"], ix.Columns);
        Assert.Equal(2, ops.Count);
    }

    [Fact]
    public void Index_NotRecreatedOnColumnRename()
    {
        var ops = Diff(d => Col(d, "note", c => c with { Name = "memo" }) with { Indexes = [d.Indexes[0] with { Columns = ["memo"] }, d.Indexes[1]] });
        Assert.Equal([new RenameColumn("note", "memo")], ops);
    }

    [Fact]
    public void Index_NewAndRemoved()
    {
        var added = new IndexDesign(null, "ix_new", false, ["a"]);
        var ops = Diff(d => d with { Indexes = [d.Indexes[1], added] });
        Assert.Equal([new DropIndex("ix_note"), new CreateIndex(added)], ops);
    }

    [Fact]
    public void ForeignKey_ActionChangeRecreates()
    {
        var ops = Diff(d => d with { ForeignKeys = [d.ForeignKeys[0] with { OnDelete = ReferentialAction.Cascade }] });
        Assert.Equal(new DropForeignKey("fk_a"), ops[0]);
        Assert.IsType<AddForeignKey>(ops[1]);
        Assert.Equal(2, ops.Count);
    }

    [Fact]
    public void ForeignKey_RemovedAndAdded()
    {
        var fk = new ForeignKeyDesign(null, "fk_new", ["a"], "public", "q", ["id"], ReferentialAction.NoAction, ReferentialAction.NoAction);
        Assert.Equal([new DropForeignKey("fk_a")], Diff(d => d with { ForeignKeys = [] }));
        Assert.Equal([new AddForeignKey(fk)], Diff(d => d with { ForeignKeys = [.. d.ForeignKeys, fk] }));
    }

    [Fact]
    public void DroppedColumnUsedByIndex_IndexDroppedAndRecreatedWithRemainingColumns()
    {
        var original = Original() with { Indexes = [new IndexDesign("ix_two", "ix_two", false, ["a", "note"])] };
        var edited = original with
        {
            Columns = [.. original.Columns.Where(c => c.Name != "note")],
            Indexes = [original.Indexes[0] with { Columns = ["a"] }],
        };
        var ops = TableDiff.Compute(original, edited);
        Assert.Equal(3, ops.Count);
        Assert.Equal(new DropIndex("ix_two"), ops[0]);
        Assert.Equal(new DropColumn("note"), ops[1]);
        Assert.Equal(["a"], Assert.IsType<CreateIndex>(ops[2]).Index.Columns);
    }

    [Fact]
    public void MixedEdit_FollowsGroupOrder()
    {
        var newCol = new ColumnDesign(null, "n", "int", true, null, false);
        var newIx = new IndexDesign(null, "ix_n", false, ["n"]);
        var newFk = new ForeignKeyDesign(null, "fk_n", ["n"], null, "q", ["id"], ReferentialAction.NoAction, ReferentialAction.NoAction);
        var edited = Original() with
        {
            Name = "o2",
            Columns = [Original().Columns[0] with { Name = "oid" }, Original().Columns[1] with { DataType = "bigint" }, newCol],
            PrimaryKey = ["a"],
            Indexes = [newIx],
            ForeignKeys = [newFk],
        };

        var ops = TableDiff.Compute(Original(), edited);

        Assert.Equal(
            [typeof(DropForeignKey), typeof(DropIndex), typeof(DropIndex), typeof(DropPrimaryKey), typeof(DropColumn), typeof(RenameColumn), typeof(AlterColumn), typeof(AddColumn), typeof(AddPrimaryKey), typeof(CreateIndex), typeof(AddForeignKey), typeof(RenameTable)],
            ops.Select(o => o.GetType()));
    }
}
