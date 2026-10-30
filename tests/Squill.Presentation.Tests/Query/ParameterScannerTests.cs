using Squill.Core;
using Squill.Presentation.Query;
using Xunit;

namespace Squill.Presentation.Tests.Query;

public sealed class ParameterScannerTests
{
    [Theory]
    [InlineData(DatabaseDialect.Sqlite, "SELECT :a, @b, $c, :a")]
    [InlineData(DatabaseDialect.SqlServer, "SELECT @a, @b, @c, @a")]
    [InlineData(DatabaseDialect.MySql, "SELECT @a, @b, @c, @a")]
    public void Scan_NamedParameters_ReturnsDistinctInOrder(DatabaseDialect dialect, string sql)
    {
        var names = ParameterScanner.Scan(sql, dialect);

        Assert.Equal(["a", "b", "c"], names);
    }

    [Theory]
    [InlineData(DatabaseDialect.Sqlite, "SELECT ':a' -- :b\n, \":c\" /* :d */")]
    [InlineData(DatabaseDialect.SqlServer, "SELECT '@a' -- @b\n/* @d */")]
    [InlineData(DatabaseDialect.PostgreSql, "SELECT ':a', $$ :b $$ -- :c")]
    public void Scan_InsideStringsAndComments_IsIgnored(DatabaseDialect dialect, string sql)
    {
        Assert.Empty(ParameterScanner.Scan(sql, dialect));
        Assert.Equal(sql, ParameterScanner.Rewrite(sql, dialect));
    }

    [Fact]
    public void Rewrite_ColonAndDollar_BecomeAt()
    {
        var sql = ParameterScanner.Rewrite("SELECT ':a', :a, $b -- :c", DatabaseDialect.Sqlite);

        Assert.Equal("SELECT ':a', @a, @b -- :c", sql);
    }

    [Theory]
    [InlineData(DatabaseDialect.Sqlite, "SELECT ?")]
    [InlineData(DatabaseDialect.Sqlite, "SELECT ?1")]
    [InlineData(DatabaseDialect.PostgreSql, "SELECT $1")]
    [InlineData(DatabaseDialect.MySql, "SELECT ?")]
    public void HasPositional_PositionalToken_ReturnsTrue(DatabaseDialect dialect, string sql)
    {
        Assert.True(ParameterScanner.HasPositional(sql, dialect));
        Assert.Empty(ParameterScanner.Scan(sql, dialect));
    }

    [Fact]
    public void HasPositional_NamedOnly_ReturnsFalse() =>
        Assert.False(ParameterScanner.HasPositional("SELECT :a", DatabaseDialect.Sqlite));
}
