using Squill.Core;
using Squill.Sql.Ddl;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class SqlQuotingTests
{
    [Theory]
    [InlineData(DatabaseDialect.PostgreSql, "a\"b", "\"a\"\"b\"")]
    [InlineData(DatabaseDialect.Sqlite, "a\"b", "\"a\"\"b\"")]
    [InlineData(DatabaseDialect.MySql, "a`b", "`a``b`")]
    [InlineData(DatabaseDialect.SqlServer, "a]b", "[a]]b]")]
    public void QuoteDoublesClosingQuote(DatabaseDialect d, string input, string expected) =>
        Assert.Equal(expected, SqlQuoting.Quote(input, d));

    [Theory]
    [InlineData("dbo", "[dbo].[t]")]
    [InlineData("", "[t]")]
    [InlineData(null, "[t]")]
    public void QualifiedNameOmitsEmptySchema(string? schema, string expected) =>
        Assert.Equal(expected, SqlQuoting.QualifiedName(schema, "t", DatabaseDialect.SqlServer));

    [Fact]
    public void LiteralDoublesSingleQuotes() => Assert.Equal("'it''s'", SqlQuoting.Literal("it's"));

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql)]
    [InlineData(DatabaseDialect.MySql)]
    [InlineData(DatabaseDialect.SqlServer)]
    [InlineData(DatabaseDialect.Sqlite)]
    public void CatalogIsNonEmptyWithoutDuplicates(DatabaseDialect d)
    {
        var types = DataTypeCatalog.For(d);
        Assert.NotEmpty(types);
        Assert.Equal(types.Count, types.Distinct(StringComparer.Ordinal).Count());
    }
}
