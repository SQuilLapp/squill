using Squill.Core;
using Squill.Sql.Formatting;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class FormatterTests
{
    private static string Fmt(string sql, DatabaseDialect dialect = DatabaseDialect.PostgreSql) =>
        SqlFormatter.Format(sql, dialect, FormatOptions.Default);

    public static TheoryData<string, string> Golden => new()
    {
        {
            "select a,b from t where x=1 and y>2 order by a desc limit 5;",
            "SELECT\n    a,\n    b\nFROM t\nWHERE x = 1 AND y > 2\nORDER BY a DESC\nLIMIT 5;"
        },
        {
            "select count(*), max(t.id) from t group by t.k having count(*)>1",
            "SELECT\n    COUNT(*),\n    MAX(t.id)\nFROM t\nGROUP BY t.k\nHAVING COUNT(*) > 1"
        },
        {
            "select a from t1 left outer join t2 on t1.id=t2.id inner join t3 on t3.id=t2.id where a is not null",
            "SELECT\n    a\nFROM t1\nLEFT OUTER JOIN t2\n    ON t1.id = t2.id\nINNER JOIN t3\n    ON t3.id = t2.id\nWHERE a IS NOT NULL"
        },
        {
            "select a from t where id in (select id from u where z=1)",
            "SELECT\n    a\nFROM t\nWHERE id IN (\n    SELECT\n        id\n    FROM u\n    WHERE z = 1\n)"
        },
        {
            "select a from t union all select b from u",
            "SELECT\n    a\nFROM t\nUNION ALL\nSELECT\n    b\nFROM u"
        },
        {
            "with c as (select 1 as n) select n from c",
            "WITH c AS (\n    SELECT\n        1 AS n\n)\nSELECT\n    n\nFROM c"
        },
        {
            "insert into t (a,b) values (1,'x'),(2,'y');",
            "INSERT INTO t (a, b)\nVALUES (1, 'x'), (2, 'y');"
        },
        {
            "update t set a=1,b=-2 where id=3",
            "UPDATE t\nSET a = 1, b = -2\nWHERE id = 3"
        },
        {
            "delete from t where id=3 returning id",
            "DELETE\nFROM t\nWHERE id = 3\nRETURNING id"
        },
        {
            "select a, -- first\n b /* second */ from t",
            "SELECT\n    a, -- first\n    b /* second */\nFROM t"
        },
        {
            "select 'from where select', \"order by\", 'it''s' from t",
            "SELECT\n    'from where select',\n    \"order by\",\n    'it''s'\nFROM t"
        },
        {
            "select a::int, x.* from x",
            "SELECT\n    a::INT,\n    x.*\nFROM x"
        },
        {
            "select 1;select 2;",
            "SELECT\n    1;\n\nSELECT\n    2;"
        },
        {
            "-- header\nselect 1",
            "-- header\n\nSELECT\n    1"
        },
        {
            "select a from t where exists (select 1 from u where u.id = t.id) and b = 2",
            "SELECT\n    a\nFROM t\nWHERE EXISTS (\n    SELECT\n        1\n    FROM u\n    WHERE u.id = t.id\n) AND b = 2"
        },
    };

    [Theory]
    [MemberData(nameof(Golden))]
    public void Format_Golden_ProducesExpectedLayoutAndIsIdempotent(string input, string expected)
    {
        var once = Fmt(input);
        Assert.Equal(expected, once);
        Assert.Equal(once, Fmt(once));
    }

    [Theory]
    [InlineData(KeywordCase.Upper, "SELECT\n    A,\n    b\nFROM T\nWHERE \"Q\" = 'x'")]
    [InlineData(KeywordCase.Lower, "select\n    A,\n    b\nfrom T\nwhere \"Q\" = 'x'")]
    [InlineData(KeywordCase.Preserve, "SeLeCt\n    A,\n    b\nFrom T\nwhere \"Q\" = 'x'")]
    public void Format_KeywordCase_OnlyChangesKeywords(KeywordCase keywordCase, string expected)
    {
        var options = new FormatOptions(keywordCase, 4);
        var result = SqlFormatter.Format("SeLeCt A,b From T where \"Q\"='x'", DatabaseDialect.PostgreSql, options);
        Assert.Equal(expected, result);
        Assert.Equal(result, SqlFormatter.Format(result, DatabaseDialect.PostgreSql, options));
    }

    [Fact]
    public void Format_IndentSize_IsHonoured()
    {
        var result = SqlFormatter.Format("select a,b from t", DatabaseDialect.PostgreSql, new FormatOptions(KeywordCase.Upper, 2));
        Assert.Equal("SELECT\n  a,\n  b\nFROM t", result);
    }

    [Fact]
    public void Format_NegativeIndent_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SqlFormatter.Format("select 1", DatabaseDialect.Sqlite, new FormatOptions(KeywordCase.Upper, -1)));

    [Fact]
    public void Format_LineComment_ForcesNewline()
    {
        var result = Fmt("select a from t -- tail\nwhere a = 1");
        Assert.Equal("SELECT\n    a\nFROM t -- tail\nWHERE a = 1", result);
    }

    [Fact]
    public void Format_UnparseableStatement_IsReturnedUnchanged()
    {
        const string Broken = "select  from   where ((";
        Assert.Equal(Broken, Fmt(Broken));
    }

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql, "select \"A b\".\"c\" from \"s\".\"t\" as \"A b\"", "SELECT\n    \"A b\".\"c\"\nFROM \"s\".\"t\" AS \"A b\"")]
    [InlineData(DatabaseDialect.Sqlite, "select `x`, [y] from t", "SELECT\n    `x`,\n    [y]\nFROM t")]
    [InlineData(DatabaseDialect.MySql, "select `a b`.c from `db`.`t` `a b`", "SELECT\n    `a b`.c\nFROM `db`.`t` `a b`")]
    [InlineData(DatabaseDialect.SqlServer, "select [a b].[c] from [dbo].[t] as [a b]", "SELECT\n    [a b].[c]\nFROM [dbo].[t] AS [a b]")]
    public void Format_QuotedIdentifiers_ArePreservedPerDialect(DatabaseDialect dialect, string input, string expected)
    {
        var result = Fmt(input, dialect);
        Assert.Equal(expected, result);
        Assert.Equal(result, Fmt(result, dialect));
    }

    [Fact]
    public void Format_EmptyText_ReturnsEmpty() => Assert.Equal(string.Empty, Fmt("  \n "));
}
