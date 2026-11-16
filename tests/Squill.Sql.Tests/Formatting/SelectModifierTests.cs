using Squill.Core;
using Squill.Sql.Formatting;
using Xunit;

namespace Squill.Sql.Tests.Formatting;

public sealed class SelectModifierTests
{
    public static TheoryData<string, DatabaseDialect, string> Rows => new()
    {
        { "SELECT TOP 10 a, b FROM t", DatabaseDialect.SqlServer, "SELECT TOP 10\n    a,\n    b\nFROM t" },
        { "SELECT TOP (10) a, b FROM t", DatabaseDialect.SqlServer, "SELECT TOP (10)\n    a,\n    b\nFROM t" },
        { "SELECT TOP 10 PERCENT a FROM t", DatabaseDialect.SqlServer, "SELECT TOP 10 PERCENT\n    a\nFROM t" },
        { "SELECT TOP (10) PERCENT WITH TIES a FROM t ORDER BY a", DatabaseDialect.SqlServer, "SELECT TOP (10) PERCENT WITH TIES\n    a\nFROM t\nORDER BY a" },
        { "SELECT DISTINCT TOP 5 a FROM t", DatabaseDialect.SqlServer, "SELECT DISTINCT TOP 5\n    a\nFROM t" },
        { "SELECT TOP (@n) a FROM t", DatabaseDialect.SqlServer, "SELECT TOP (@n)\n    a\nFROM t" },
        { "SELECT DISTINCT ON (a) a, b FROM t", DatabaseDialect.PostgreSql, "SELECT DISTINCT ON (a)\n    a,\n    b\nFROM t" },
        { "SELECT ALL a FROM t", DatabaseDialect.PostgreSql, "SELECT ALL\n    a\nFROM t" },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void Modifier_stays_on_the_select_line_and_formatting_is_idempotent(string sql, DatabaseDialect dialect, string expected)
    {
        var once = SqlFormatter.Format(sql, dialect, FormatOptions.Default);
        Assert.Equal(expected, once);
        Assert.Equal(once, SqlFormatter.Format(once, dialect, FormatOptions.Default));
    }

    [Fact]
    public void Lower_case_option_applies_to_modifiers()
    {
        var options = FormatOptions.Default with { KeywordCase = KeywordCase.Lower };
        Assert.Equal("select top (10) percent\n    a\nfrom t", SqlFormatter.Format("SELECT TOP (10) PERCENT a FROM t", DatabaseDialect.SqlServer, options));
    }
}
