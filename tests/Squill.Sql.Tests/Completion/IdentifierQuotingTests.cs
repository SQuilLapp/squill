using Squill.Core;
using Squill.Sql.Completion;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class IdentifierQuotingTests
{
    [Theory]
    [InlineData(DatabaseDialect.PostgreSql, "order", "\"order\"")]
    [InlineData(DatabaseDialect.PostgreSql, "user", "\"user\"")]
    [InlineData(DatabaseDialect.PostgreSql, "Select", "\"Select\"")]
    [InlineData(DatabaseDialect.PostgreSql, "group", "\"group\"")]
    [InlineData(DatabaseDialect.PostgreSql, "table", "\"table\"")]
    [InlineData(DatabaseDialect.MySql, "order", "`order`")]
    [InlineData(DatabaseDialect.MySql, "user", "`user`")]
    [InlineData(DatabaseDialect.MySql, "Select", "`Select`")]
    [InlineData(DatabaseDialect.MySql, "group", "`group`")]
    [InlineData(DatabaseDialect.MySql, "table", "`table`")]
    [InlineData(DatabaseDialect.SqlServer, "order", "[order]")]
    [InlineData(DatabaseDialect.SqlServer, "user", "[user]")]
    [InlineData(DatabaseDialect.SqlServer, "Select", "[Select]")]
    [InlineData(DatabaseDialect.SqlServer, "group", "[group]")]
    [InlineData(DatabaseDialect.SqlServer, "table", "[table]")]
    [InlineData(DatabaseDialect.Sqlite, "order", "\"order\"")]
    [InlineData(DatabaseDialect.Sqlite, "user", "\"user\"")]
    [InlineData(DatabaseDialect.Sqlite, "group", "\"group\"")]
    public void Keyword_IsQuoted(DatabaseDialect dialect, string name, string expected)
    {
        Assert.Equal(expected, IdentifierQuoting.Quote(name, dialect));
    }

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql)]
    [InlineData(DatabaseDialect.MySql)]
    [InlineData(DatabaseDialect.SqlServer)]
    [InlineData(DatabaseDialect.Sqlite)]
    public void PlainName_StaysBare(DatabaseDialect dialect)
    {
        Assert.Equal("orders", IdentifierQuoting.Quote("orders", dialect));
        Assert.Equal("customer_id", IdentifierQuoting.Quote("customer_id", dialect));
    }
}
