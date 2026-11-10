using Squill.Core;
using Squill.Sql.Completion;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class TableReferenceTests
{
    private static TableInfo T(string? schema, string name) =>
        new(schema, name, TableKind.Table, [new ColumnDefinition("id", "int", false, true, null)], [], []);

    private static SchemaSnapshot Snap(string? def, params TableInfo[] tables) => new(def, tables);

    private static string Insert(SchemaSnapshot s, int index, DatabaseDialect d = DatabaseDialect.PostgreSql) =>
        TableReference.Insert(s.Tables[index], s, d);

    [Fact]
    public void DefaultSchemaTable_IsBare()
    {
        Assert.Equal("users", Insert(Snap("public", T("public", "users"), T("audit", "log")), 0));
    }

    [Fact]
    public void UniqueNameInOtherSchema_IsQualified()
    {
        // The engine resolves a bare name only in the default schema (search_path, default schema): found by running it on PostgreSQL.
        Assert.Equal("audit.log", Insert(Snap("public", T("public", "users"), T("audit", "log")), 1));
    }

    [Fact]
    public void SameNameInTwoSchemas_DefaultBareOtherQualified()
    {
        var s = Snap("public", T("public", "log"), T("audit", "log"));
        Assert.Equal("log", Insert(s, 0));
        Assert.Equal("audit.log", Insert(s, 1));
    }

    [Fact]
    public void NoDefaultSchema_UniqueNameIsBare()
    {
        Assert.Equal("log", Insert(Snap(null, T("main", "log"), T("main", "users")), 0));
    }

    [Fact]
    public void NoDefaultSchema_DuplicateNamesAreBothQualified()
    {
        var s = Snap(null, T("a", "log"), T("b", "log"));
        Assert.Equal("a.log", Insert(s, 0));
        Assert.Equal("b.log", Insert(s, 1));
    }

    [Fact]
    public void TableWithoutSchema_IsBare()
    {
        Assert.Equal("t", Insert(Snap(null, T(null, "t"), T(null, "t")), 0));
    }

    [Fact]
    public void Quoting_QuotesIdentifiersThatNeedIt()
    {
        var s = Snap("public", T("public", "Log"), T("Audit", "Log"));
        Assert.Equal("\"Audit\".\"Log\"", Insert(s, 1));
        var mysql = Snap("public", T("public", "a b"), T("au dit", "a b"));
        Assert.Equal("`au dit`.`a b`", Insert(mysql, 1, DatabaseDialect.MySql));
        Assert.Equal("[au dit].[a b]", Insert(mysql, 1, DatabaseDialect.SqlServer));
    }
}
