using Squill.Core;
using Squill.Sql.Analysis;
using Squill.Sql.Syntax;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class ScopeResolverTests
{
    /// <summary>Resolves at the position of the <c>|</c> marker, which is removed from the text.</summary>
    private static ScopeInfo At(string marked, DatabaseDialect dialect = DatabaseDialect.PostgreSql)
    {
        var offset = marked.IndexOf('|');
        var script = SqlParser.Parse(marked.Remove(offset, 1), dialect);
        return SqlScopeResolver.Resolve(script, offset);
    }

    [Theory]
    [InlineData("select | from t", ClauseKind.Select)]
    [InlineData("select a.| from t a", ClauseKind.Select)]
    [InlineData("select * from |", ClauseKind.From)]
    [InlineData("select * from t, |", ClauseKind.From)]
    [InlineData("select * from a join |", ClauseKind.Join)]
    [InlineData("select * from a join b on |", ClauseKind.On)]
    [InlineData("select * from t where |", ClauseKind.Where)]
    [InlineData("select * from t where a = 1 group by |", ClauseKind.GroupBy)]
    [InlineData("select a from t group by a having |", ClauseKind.Having)]
    [InlineData("select a from t order by |", ClauseKind.OrderBy)]
    [InlineData("select a from t limit |", ClauseKind.Other)]
    [InlineData("update t set |", ClauseKind.Set)]
    [InlineData("update t set a = 1 where |", ClauseKind.Where)]
    [InlineData("insert into t values (|", ClauseKind.Values)]
    [InlineData("insert into t (|", ClauseKind.Other)]
    [InlineData("delete from t where |", ClauseKind.Where)]
    [InlineData("select * from t where f(|)", ClauseKind.Where)]
    public void ClauseIsDetected(string marked, ClauseKind expected) => Assert.Equal(expected, At(marked).Clause);

    [Fact]
    public void AliasesAndJoinsAreVisible()
    {
        var scope = At("select | from public.users u join orders o on o.uid = u.id, items");
        Assert.Equal(
            [("public", "users", "u"), (null, "orders", "o"), (null, "items", null)],
            scope.Sources.Select(s => (s.Schema, s.Name, s.Alias)));
        Assert.All(scope.Sources, s => Assert.Null(s.DerivedColumns));
    }

    [Fact]
    public void CteIsVisibleInMainQueryWithItsColumns()
    {
        var scope = At("with c as (select a, b as x, 1 from t) select | from c");
        var source = Assert.Single(scope.Sources);
        Assert.Equal("c", source.Name);
        Assert.Equal(["a", "x"], source.DerivedColumns);
    }

    [Fact]
    public void CteDeclaredColumnsWinAndStarMeansUnknown()
    {
        Assert.Equal(["p", "q"], Assert.Single(At("with c(p, q) as (select 1, 2) select | from c").Sources).DerivedColumns);
        Assert.Null(Assert.Single(At("with c as (select * from t) select | from c").Sources).DerivedColumns);
    }

    [Fact]
    public void CteIsVisibleInLaterCteOnly()
    {
        var later = At("with a as (select 1 x), b as (select | from a) select * from b");
        Assert.Equal("a", later.Sources[0].Name);
        var first = At("with a as (select | from b), b as (select 1) select * from a");
        Assert.Equal("b", first.Sources[0].Name);
        Assert.Null(first.Sources[0].DerivedColumns);
    }

    [Fact]
    public void DerivedTableExposesItsColumns()
    {
        var source = Assert.Single(At("select | from (select id, name n from t) d").Sources);
        Assert.Equal("d", source.Alias);
        Assert.Equal(["id", "n"], source.DerivedColumns);
    }

    [Fact]
    public void CorrelatedSubqueryListsInnerThenOuterSources()
    {
        var scope = At("select * from a x where exists (select 1 from b y where y.k = |)");
        Assert.Equal(ClauseKind.Where, scope.Clause);
        Assert.Equal(["y", "x"], scope.Sources.Select(s => s.Alias));
    }

    [Fact]
    public void CaretAfterClosedSubqueryBelongsToOuterStatement()
    {
        var scope = At("select * from a where x in (select 1 from b) and |");
        Assert.Equal(ClauseKind.Where, scope.Clause);
        Assert.Equal(["a"], scope.Sources.Select(s => s.Name));
    }

    [Fact]
    public void UnionBranchesHaveTheirOwnSources()
    {
        Assert.Equal(["b"], At("select 1 from a union select | from b").Sources.Select(s => s.Name));
        Assert.Equal(["a"], At("select | from a union select 1 from b").Sources.Select(s => s.Name));
    }

    [Fact]
    public void DmlTargetsAreSources()
    {
        Assert.Equal(["t"], At("update t set a = |").Sources.Select(s => s.Name));
        Assert.Equal(["t", "s"], At("update t set a = 1 from s where |").Sources.Select(s => s.Name));
        Assert.Equal(["t"], At("delete from t where |").Sources.Select(s => s.Name));
        Assert.Equal(["t"], At("insert into t (|").Sources.Select(s => s.Name));
        Assert.Equal(["t", "u"], At("delete t from t join u on |", DatabaseDialect.SqlServer).Sources.Select(s => s.Name));
    }

    [Fact]
    public void SecondStatementIsIndependent()
    {
        var scope = At("select * from a; select | from b");
        Assert.Equal(["b"], scope.Sources.Select(s => s.Name));
    }

    [Fact]
    public void OutsideAnyStatementIsNone()
    {
        Assert.Equal(ClauseKind.None, At("select 1; |").Clause);
        Assert.Equal(ClauseKind.None, At("|").Clause);
        Assert.Empty(At("|").Sources);
    }

    [Fact]
    public void OutOfRangeOffsetIsClamped()
    {
        var script = SqlParser.Parse("select a from t", DatabaseDialect.PostgreSql);
        Assert.Equal(ClauseKind.From, SqlScopeResolver.Resolve(script, 999).Clause);
        Assert.Equal(ClauseKind.None, SqlScopeResolver.Resolve(script, -5).Clause);
    }
}
