using Squill.Core;
using Squill.Sql.Completion;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class JoinKeywordTests
{
    private static string[] Labels(string marked, DatabaseDialect dialect = DatabaseDialect.PostgreSql)
    {
        var offset = marked.IndexOf('|', StringComparison.Ordinal);
        return [.. new KeywordCompletionProvider().GetCompletions(new SqlRequest(marked.Remove(offset, 1), offset, dialect, null)).Select(i => i.Label)];
    }

    private static string[] Joins(DatabaseDialect dialect) => [.. Labels("select * from a |", dialect).Where(l => l.Contains("JOIN", StringComparison.Ordinal) || l.Contains("APPLY", StringComparison.Ordinal))];

    [Fact]
    public void PostgreSqlJoins()
        => Assert.Equal(["JOIN", "INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "FULL JOIN", "CROSS JOIN", "NATURAL JOIN", "JOIN LATERAL", "LEFT JOIN LATERAL"], Joins(DatabaseDialect.PostgreSql));

    [Fact]
    public void MySqlHasNoFullJoin()
        => Assert.Equal(["JOIN", "INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "CROSS JOIN", "NATURAL JOIN", "STRAIGHT_JOIN"], Joins(DatabaseDialect.MySql));

    [Fact]
    public void SqlServerHasApplyAndNoNatural()
        => Assert.Equal(["JOIN", "INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "FULL JOIN", "CROSS JOIN", "CROSS APPLY", "OUTER APPLY"], Joins(DatabaseDialect.SqlServer));

    [Fact]
    public void SqliteJoins()
        => Assert.Equal(["JOIN", "INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "FULL JOIN", "CROSS JOIN", "NATURAL JOIN"], Joins(DatabaseDialect.Sqlite));

    [Fact]
    public void OracleJoins()
        => Assert.Equal(["JOIN", "INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "FULL JOIN", "CROSS JOIN", "NATURAL JOIN", "CROSS APPLY", "OUTER APPLY"], Joins(DatabaseDialect.Oracle));

    [Theory]
    [InlineData("select * from a cross join b |")]
    [InlineData("select * from a natural join b |")]
    public void NoConditionAfterCrossOrNatural(string marked)
    {
        var labels = Labels(marked);
        Assert.DoesNotContain("ON", labels);
        Assert.DoesNotContain("USING", labels);
    }

    [Fact]
    public void NoConditionAfterApply()
        => Assert.DoesNotContain("ON", Labels("select * from a cross apply b |", DatabaseDialect.SqlServer));

    [Fact]
    public void ConditionAfterLeftJoin()
    {
        Assert.Contains("ON", Labels("select * from a left join b |"));
        Assert.Contains("USING", Labels("select * from a left join b |"));
        Assert.Contains("ON", Labels("select * from a left join b |", DatabaseDialect.SqlServer));
        Assert.DoesNotContain("USING", Labels("select * from a left join b |", DatabaseDialect.SqlServer));
    }

    [Fact]
    public void LaterJoinAfterCrossJoinTakesCondition()
        => Assert.Contains("ON", Labels("select * from a cross join b join c |"));

    [Fact]
    public void AfterOnConditionAndCommaUnchanged()
    {
        Assert.Contains("WHERE", Labels("select * from a join b on x = y |"));
        Assert.Empty(Labels("select * from a, |", DatabaseDialect.Sqlite));
    }
}
