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
}
