using Squill.Core;
using Squill.Presentation.Editor;
using Xunit;

namespace Squill.Presentation.Tests.Explorer;

public sealed class SqlSnippetsTests
{
    private static TableInfo Table(string? schema, string name) => new(schema, name, TableKind.Table, [], [], []);

    [Theory]
    [InlineData(DatabaseDialect.SqlServer, "SELECT TOP 100 * FROM [dbo].[t]")]
    [InlineData(DatabaseDialect.PostgreSql, "SELECT * FROM \"dbo\".\"t\" LIMIT 100")]
    [InlineData(DatabaseDialect.Sqlite, "SELECT * FROM \"dbo\".\"t\" LIMIT 100")]
    [InlineData(DatabaseDialect.MySql, "SELECT * FROM `dbo`.`t` LIMIT 100")]
    public void SelectTop_PerDialect_UsesDialectSyntax(DatabaseDialect d, string expected) =>
        Assert.Equal(expected, SqlSnippets.SelectTop(Table("dbo", "t"), d));

    [Fact]
    public void SelectTop_NoSchemaAndCustomCount_OmitsSchema() =>
        Assert.Equal("SELECT * FROM \"t\" LIMIT 5", SqlSnippets.SelectTop(Table(null, "t"), DatabaseDialect.Sqlite, 5));

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql, "a\"b", "\"a\"\"b\"")]
    [InlineData(DatabaseDialect.MySql, "a`b", "`a``b`")]
    [InlineData(DatabaseDialect.SqlServer, "a]b", "[a]]b]")]
    public void Quote_EmbeddedQuoteChar_IsDoubled(DatabaseDialect d, string id, string expected) =>
        Assert.Equal(expected, SqlSnippets.Quote(id, d));
}
