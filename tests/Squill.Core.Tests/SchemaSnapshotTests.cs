using Xunit;

namespace Squill.Core.Tests;

public sealed class SchemaSnapshotTests
{
    private static TableInfo T(string? schema, string name) => new(schema, name, TableKind.Table, [], [], []);

    [Fact]
    public void FindTable_DifferentCase_Matches()
    {
        var snap = new SchemaSnapshot("public", [T("public", "Users")]);
        Assert.NotNull(snap.FindTable("PUBLIC", "users"));
    }

    [Fact]
    public void FindTable_NoSchema_PrefersDefaultSchema()
    {
        var snap = new SchemaSnapshot("b", [T("a", "t"), T("b", "t")]);
        Assert.Equal("b", snap.FindTable(null, "t")!.Schema);
    }

    [Fact]
    public void FindTable_NoSchemaAmbiguous_ReturnsNull()
    {
        var snap = new SchemaSnapshot("x", [T("a", "t"), T("b", "t")]);
        Assert.Null(snap.FindTable(null, "t"));
    }

    [Fact]
    public void FindTable_NoSchemaUniqueElsewhere_Matches()
    {
        var snap = new SchemaSnapshot("x", [T("a", "t")]);
        Assert.Equal("a", snap.FindTable(null, "t")!.Schema);
    }

    [Fact]
    public void FindTable_ExplicitSchemaMismatch_ReturnsNull()
    {
        var snap = new SchemaSnapshot(null, [T("a", "t")]);
        Assert.Null(snap.FindTable("b", "t"));
        Assert.Null(SchemaSnapshot.Empty.FindTable(null, "t"));
    }

    [Fact]
    public void NewMembers_DefaultToEmpty()
    {
        var snap = new SchemaSnapshot(null, [T(null, "t")]);
        Assert.Empty(snap.Routines);
        Assert.Empty(snap.Triggers);
        Assert.Empty(SchemaSnapshot.Empty.Routines);
        Assert.Null(snap.Tables[0].Definition);
    }

    [Fact]
    public void RoutinesTriggersAndDefinition_AreCarried_AndFindTableIgnoresThem()
    {
        var mv = new TableInfo("public", "mv", TableKind.MaterializedView, [], [], [], Definition: "SELECT 1");
        var snap = new SchemaSnapshot("public", [mv])
        {
            Routines = [new RoutineInfo("public", "add", RoutineKind.Function, "add(integer, integer)", "integer", "select $1 + $2")],
            Triggers = [new TriggerInfo("public", "mv", "trg", "AFTER", "INSERT OR UPDATE", null)],
        };
        Assert.Same(mv, snap.FindTable(null, "mv"));
        Assert.Equal("SELECT 1", mv.Definition);
        Assert.Equal(RoutineKind.Function, snap.Routines[0].Kind);
        Assert.Equal("trg", snap.Triggers[0].Name);
    }

    [Theory]
    [InlineData(TableKind.Table, false)]
    [InlineData(TableKind.View, true)]
    [InlineData(TableKind.MaterializedView, true)]
    public void IsViewLike_MatchesKind(TableKind kind, bool expected) => Assert.Equal(expected, kind.IsViewLike());
}
