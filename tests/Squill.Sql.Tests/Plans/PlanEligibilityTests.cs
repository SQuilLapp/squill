using Squill.Core;
using Squill.Core.Plans;
using Squill.Sql.Plans;
using Xunit;

namespace Squill.Sql.Tests.Plans;

public sealed class PlanEligibilityTests
{
    private const DatabaseDialect Pg = DatabaseDialect.PostgreSql;
    private const DatabaseDialect My = DatabaseDialect.MySql;
    private const DatabaseDialect Ms = DatabaseDialect.SqlServer;
    private const DatabaseDialect Lite = DatabaseDialect.Sqlite;

    [Theory]
    [InlineData("select * from t", Pg)]
    [InlineData("SELECT 1", My)]
    [InlineData("select 1 -- c", Ms)]
    [InlineData("/* lead */ -- more\n  select * from t;", Pg)]
    [InlineData("with a as (select 1) select * from a", Ms)]
    [InlineData("values (1), (2)", Pg)]
    [InlineData("table t", My)]
    [InlineData("select * from t for update", Pg)]
    [InlineData("select * from t for no key update", Pg)]
    [InlineData("select * from t for share", My)]
    [InlineData("select insert('abc', 1, 1, 'x')", My)]
    [InlineData("(select 1) union (select 2)", Pg)]
    public void ReadOnly_CanExplainAndAnalyze(string sql, DatabaseDialect dialect)
    {
        var e = PlanEligibility.Evaluate(sql, dialect);

        Assert.True(e.CanExplain);
        Assert.True(e.CanAnalyze);
        Assert.Null(e.ExplainReasonCode);
        Assert.Null(e.AnalyzeReasonCode);
    }

    [Theory]
    [InlineData("with d as (delete from t returning *) select * from d", Pg)]
    [InlineData("select * into t2 from t", Ms)]
    [InlineData("select * into t2 from t", Pg)]
    [InlineData("insert into t values (1)", Pg)]
    [InlineData("update t set a = 1", My)]
    [InlineData("delete from t", Ms)]
    [InlineData("merge into t using s on 1=1 when matched then delete", Pg)]
    [InlineData("replace into t values (1)", My)]
    public void Writes_CanExplainNotAnalyze(string sql, DatabaseDialect dialect)
    {
        var e = PlanEligibility.Evaluate(sql, dialect);

        Assert.True(e.CanExplain);
        Assert.False(e.CanAnalyze);
        Assert.Null(e.ExplainReasonCode);
        Assert.Equal(PlanMessageCodes.ReasonWrites, e.AnalyzeReasonCode);
    }

    [Theory]
    [InlineData("exec sp_who", Ms, PlanMessageCodes.ReasonNotExplainable)]
    [InlineData("create table t (a int)", Pg, PlanMessageCodes.ReasonNotExplainable)]
    [InlineData("drop table t", My, PlanMessageCodes.ReasonNotExplainable)]
    [InlineData("pragma foreign_keys = on", Lite, PlanMessageCodes.ReasonNotExplainable)]
    [InlineData("set x = 1", Ms, PlanMessageCodes.ReasonNotExplainable)]
    [InlineData("begin", Pg, PlanMessageCodes.ReasonNotExplainable)]
    [InlineData("table t", Ms, PlanMessageCodes.ReasonNotExplainable)]
    [InlineData("merge into t using s on 1=1 when matched then delete", My, PlanMessageCodes.ReasonNotExplainable)]
    [InlineData("explain select 1", Pg, PlanMessageCodes.ReasonAlreadyExplain)]
    [InlineData("EXPLAIN ANALYZE select 1", My, PlanMessageCodes.ReasonAlreadyExplain)]
    [InlineData("set showplan_xml on", Ms, PlanMessageCodes.ReasonAlreadyExplain)]
    [InlineData("", Pg, PlanMessageCodes.ReasonEmpty)]
    [InlineData("   \n ", Ms, PlanMessageCodes.ReasonEmpty)]
    [InlineData("-- only a comment", Pg, PlanMessageCodes.ReasonEmpty)]
    [InlineData("/* c */ ;", My, PlanMessageCodes.ReasonEmpty)]
    public void Refused_BothActionsGetTheReason(string sql, DatabaseDialect dialect, string code)
    {
        var e = PlanEligibility.Evaluate(sql, dialect);

        Assert.False(e.CanExplain);
        Assert.False(e.CanAnalyze);
        Assert.Equal(code, e.ExplainReasonCode);
        Assert.Equal(code, e.AnalyzeReasonCode);
    }

    [Theory]
    [InlineData("select 1")]
    [InlineData("with a as (select 1) select * from a")]
    [InlineData("insert into t values (1)")]
    public void Sqlite_AnalyzeAlwaysUnavailable(string sql)
    {
        var e = PlanEligibility.Evaluate(sql, Lite);

        Assert.True(e.CanExplain);
        Assert.False(e.CanAnalyze);
        Assert.Equal(PlanMessageCodes.ReasonNoAnalyze, e.AnalyzeReasonCode);
    }

    [Fact]
    public void SeveralStatements_OnlyTheFirstCounts()
    {
        var e = PlanEligibility.Evaluate("select 1; delete from t", Pg);

        Assert.True(e.CanAnalyze);
    }
}
