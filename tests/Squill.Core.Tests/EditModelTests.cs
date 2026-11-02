using Squill.Core.Editing;
using Xunit;

namespace Squill.Core.Tests;

public sealed class EditModelTests
{
    private static readonly ColumnDefinition Id = new("id", "integer", false, true, null, true);
    private static readonly ColumnDefinition Name = new("name", "text", true, false, null);

    private static EditTarget Target() => new(
        null, "t", [new EditColumn(0, Id), new EditColumn(2, Name)], ["id"],
        new TableInfo(null, "t", TableKind.Table, [Id, Name], [], []));

    [Fact]
    public void ByOrdinal_MappedOrdinal_ReturnsColumn() => Assert.Equal(Name, Target().ByOrdinal(2)!.Definition);

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(99)]
    public void ByOrdinal_UnmappedOrOutOfRange_ReturnsNull(int ordinal) => Assert.Null(Target().ByOrdinal(ordinal));

    [Fact]
    public void IsEditable_MappedAndUnmapped_Matches()
    {
        Assert.True(Target().IsEditable(0));
        Assert.False(Target().IsEditable(1));
    }

    [Fact]
    public void Records_SameValues_AreEqual()
    {
        Assert.Equal(new ColumnValue("a", 1), new ColumnValue("a", 1));
        Assert.Equal(new EditIssue(3, "a", "bad"), new EditIssue(3, "a", "bad"));
        Assert.Equal(new EditColumn(0, Id), new EditColumn(0, Id));
        IReadOnlyList<ColumnValue> key = [new ColumnValue("id", 1)];
        Assert.Equal<RowEdit>(new DeleteRow(key), new DeleteRow(key));
        Assert.NotEqual<RowEdit>(new DeleteRow(key), new InsertRow(key));
    }
}
