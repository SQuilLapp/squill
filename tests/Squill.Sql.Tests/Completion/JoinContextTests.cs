using Squill.Core;
using Squill.Sql.Completion;
using Squill.Sql.Syntax;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class JoinContextTests
{
    /// <summary>Analyzes at the <c>|</c> marker; a word glued to it (<c>ord|</c>) is the typed word.</summary>
    private static JoinContext At(string marked, DatabaseDialect dialect = DatabaseDialect.PostgreSql)
    {
        var offset = marked.IndexOf('|');
        var text = marked.Remove(offset, 1);
        var start = offset;
        while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
        {
            start--;
        }

        return JoinContextAnalyzer.Analyze(new SqlRequest(text, offset, dialect, null), start, offset);
    }

    [Theory]
    [InlineData("select * from a join |", JoinKind.Inner)]
    [InlineData("select * from a inner join |", JoinKind.Inner)]
    [InlineData("select * from a left join |", JoinKind.Left)]
    [InlineData("select * from a left outer join |", JoinKind.Left)]
    [InlineData("select * from a right join |", JoinKind.Right)]
    [InlineData("select * from a full outer join |", JoinKind.Full)]
    [InlineData("select * from a cross join |", JoinKind.Cross)]
    [InlineData("select * from a left /* c */ join |", JoinKind.Left)]
    [InlineData("select * from a, |", JoinKind.Comma)]
    public void Kind(string marked, JoinKind expected)
    {
        var context = At(marked);
        Assert.True(context.IsJoin);
        Assert.Equal(expected, context.Kind);
        Assert.False(context.IsNatural);
        Assert.False(context.HasCondition);
        Assert.Equal(["a"], context.LeftSources.Select(s => s.Name));
    }

    [Fact]
    public void Natural()
    {
        var context = At("select * from a natural join |");
        Assert.True(context.IsNatural);
        Assert.Equal(JoinKind.Inner, context.Kind);
        Assert.True(At("select * from a natural left join |").IsNatural);
    }

    [Theory]
    [InlineData("select * from a cross apply |", JoinKind.Cross)]
    [InlineData("select * from a outer apply |", JoinKind.Left)]
    public void Apply_OnSqlServer(string marked, JoinKind expected)
    {
        var context = At(marked, DatabaseDialect.SqlServer);
        Assert.True(context.IsApply);
        Assert.Equal(expected, context.Kind);
    }

    [Fact]
    public void HalfTypedWord_IsNotASource()
    {
        var context = At("select * from a join ord|");
        Assert.True(context.IsJoin);
        Assert.Equal(["a"], context.LeftSources.Select(s => s.Name));
    }

    [Fact]
    public void TwoJoins_GiveTwoLeftSources()
    {
        var context = At("select * from a join b on x = y join |");
        Assert.Equal(["a", "b"], context.LeftSources.Select(s => s.Name));
    }

    [Fact]
    public void Subquery_KeepsOnlyItsOwnSources()
    {
        var context = At("select (select 1 from c join |) from a");
        Assert.True(context.IsJoin);
        Assert.Equal(["c"], context.LeftSources.Select(s => s.Name));
    }

    [Theory]
    [InlineData("select * from a join | on a.x = b.y")]
    [InlineData("select * from a join ord| on a.x = b.y")]
    [InlineData("select * from a join | using (x)")]
    public void ExistingCondition(string marked) => Assert.True(At(marked).HasCondition);

    [Fact]
    public void Lateral_IsSkipped() => Assert.True(At("select * from a left join lateral |").IsJoin);

    [Theory]
    [InlineData("select * from |")]
    [InlineData("select * from ord|")]
    [InlineData("insert into |")]
    [InlineData("update |")]
    [InlineData("|")]
    [InlineData("select a, |")]
    public void OtherPositions_AreNotJoins(string marked)
    {
        var context = At(marked);
        Assert.False(context.IsJoin);
        Assert.Empty(context.LeftSources);
    }
}
