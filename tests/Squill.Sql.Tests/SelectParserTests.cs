using Squill.Core;
using Squill.Sql.Syntax;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class SelectParserTests
{
    private static SelectStatement Select(string sql, DatabaseDialect dialect = DatabaseDialect.PostgreSql, bool clean = true)
    {
        var script = SqlParser.Parse(sql, dialect);
        if (clean)
        {
            Assert.Empty(script.Diagnostics);
        }

        var statement = Assert.Single(script.Statements);
        AssertInside(statement, sql.Length);
        return Assert.IsType<SelectStatement>(statement);
    }

    private static void AssertInside(SyntaxNode node, int length)
    {
        Assert.InRange(node.Start, 0, length);
        Assert.InRange(node.End, node.Start, length);
        switch (node)
        {
            case SelectStatement s:
                s.Items.ToList().ForEach(i => AssertInside(i, length));
                s.Ctes.ToList().ForEach(c => AssertInside(c, length));
                s.OrderBy.ToList().ForEach(o => AssertInside(o, length));
                if (s.From is not null)
                {
                    AssertInside(s.From, length);
                }

                break;
            case JoinSource j:
                AssertInside(j.Left, length);
                AssertInside(j.Right, length);
                break;
            case SetOperation o:
                AssertInside(o.Left, length);
                AssertInside(o.Right, length);
                break;
            case SubquerySource q:
                AssertInside(q.Query, length);
                break;
            case CommonTableExpr c:
                AssertInside(c.Query, length);
                break;
        }
    }

    [Fact]
    public void Parse_FullSelect_FillsEveryClause()
    {
        var sql = "SELECT DISTINCT a, b AS x, count(*) n FROM s.t AS u WHERE a > 1 GROUP BY a, b HAVING count(*) > 2 ORDER BY a DESC, b LIMIT 10 OFFSET 5";
        var s = Select(sql);
        Assert.True(s.Distinct);
        Assert.Equal(new string?[] { null, "x", "n" }, s.Items.Select(i => i.Alias));
        var from = Assert.IsType<TableRef>(s.From);
        Assert.Equal(("s", "t", "u"), (from.Schema, from.Name, from.Alias));
        Assert.NotNull(s.Where);
        Assert.Equal(2, s.GroupBy.Count);
        Assert.NotNull(s.Having);
        Assert.Equal([true, false], s.OrderBy.Select(o => o.Descending));
        Assert.NotNull(s.Limit);
        Assert.NotNull(s.Offset);
        Assert.Equal(0, s.Start);
        Assert.Equal(sql.Length, s.Length);
    }

    [Fact]
    public void Parse_Ctes_AreAttachedWithColumns()
    {
        var sql = "WITH a(x, y) AS (SELECT 1, 2), b AS (SELECT * FROM a) SELECT * FROM b";
        var s = Select(sql);
        Assert.Equal(["a", "b"], s.Ctes.Select(c => c.Name));
        Assert.Equal(["x", "y"], s.Ctes[0].Columns);
        Assert.IsType<SelectStatement>(s.Ctes[1].Query);
        Assert.Equal(0, s.Start);
        Assert.Equal(sql.Length, s.End);
    }

    [Fact]
    public void Parse_JoinsWithAliases_AreLeftAssociative()
    {
        var s = Select("SELECT * FROM a x INNER JOIN b y ON x.id = y.id LEFT OUTER JOIN c ON c.id = y.id, d CROSS JOIN e");
        var cross = Assert.IsType<JoinSource>(s.From);
        Assert.Equal(JoinKind.Cross, cross.Kind);
        var comma = Assert.IsType<JoinSource>(cross.Left);
        Assert.Equal(JoinKind.Comma, comma.Kind);
        var left = Assert.IsType<JoinSource>(comma.Left);
        Assert.Equal(JoinKind.Left, left.Kind);
        Assert.NotNull(left.On);
        var inner = Assert.IsType<JoinSource>(left.Left);
        Assert.Equal(JoinKind.Inner, inner.Kind);
        Assert.Equal("x", Assert.IsType<TableRef>(inner.Left).Alias);
        Assert.Equal("y", Assert.IsType<TableRef>(inner.Right).Alias);
    }

    [Fact]
    public void Parse_ThreePartName_KeepsLastTwo()
    {
        var from = Assert.IsType<TableRef>(Select("SELECT * FROM db.s.t").From);
        Assert.Equal(("s", "t"), (from.Schema, from.Name));
    }

    [Fact]
    public void Parse_SubqueryInFromAndWhere()
    {
        var s = Select("SELECT * FROM (SELECT a FROM t) q WHERE a IN (SELECT b FROM u)");
        var source = Assert.IsType<SubquerySource>(s.From);
        Assert.Equal("q", source.Alias);
        Assert.IsType<SelectStatement>(source.Query);
        var where = Assert.IsType<BinaryExpr>(s.Where);
        Assert.IsType<SelectStatement>(Assert.IsType<SubqueryExpr>(where.Right).Query);
    }

    [Fact]
    public void Parse_Union_IsLeftAssociative()
    {
        var sql = "SELECT 1 UNION ALL SELECT 2 EXCEPT SELECT 3";
        var statement = Assert.Single(SqlParser.Parse(sql, DatabaseDialect.PostgreSql).Statements);
        var except = Assert.IsType<SetOperation>(statement);
        Assert.Equal(SetOperator.Except, except.Op);
        var union = Assert.IsType<SetOperation>(except.Left);
        Assert.True(union.All);
        Assert.Equal(sql.Length, except.Length);
    }

    [Fact]
    public void Parse_TopOnlyForSqlServerAndLimitElsewhere()
    {
        var top = Select("SELECT TOP 5 a FROM t", DatabaseDialect.SqlServer);
        Assert.NotNull(top.Top);
        Assert.Null(top.Limit);
        Assert.Single(top.Items);

        var limit = Select("SELECT a FROM t LIMIT 5", DatabaseDialect.MySql);
        Assert.Null(limit.Top);
        Assert.NotNull(limit.Limit);

        var mysql = Select("SELECT a FROM t LIMIT 5, 10", DatabaseDialect.MySql);
        Assert.NotNull(mysql.Offset);
        Assert.NotNull(mysql.Limit);
    }

    [Fact]
    public void Parse_TSqlOffsetFetch_FillsOffsetOnly()
    {
        var s = Select("SELECT a FROM t ORDER BY a OFFSET 10 ROWS FETCH NEXT 5 ROWS ONLY", DatabaseDialect.SqlServer);
        Assert.NotNull(s.Offset);
        Assert.Null(s.Limit);
    }

    [Fact]
    public void Parse_DotAfterQualifier_StillHasFrom()
    {
        var s = Select("select a. from t", clean: false);
        Assert.Equal("t", Assert.IsType<TableRef>(s.From).Name);
        Assert.Equal("", Assert.IsType<ColumnRef>(s.Items[0].Value).Name);
    }

    [Fact]
    public void Parse_EmptySelectList_StillHasFrom()
    {
        var sql = "select from t";
        var script = SqlParser.Parse(sql, DatabaseDialect.PostgreSql);
        Assert.NotEmpty(script.Diagnostics);
        var s = Assert.IsType<SelectStatement>(Assert.Single(script.Statements));
        Assert.Empty(s.Items);
        Assert.Equal("t", Assert.IsType<TableRef>(s.From).Name);
    }

    [Theory]
    [InlineData("select * from")]
    [InlineData("select * from where a = 1")]
    [InlineData("select * from s.")]
    public void Parse_MissingTable_GivesEmptyTableRefAndDiagnostic(string sql)
    {
        var script = SqlParser.Parse(sql, DatabaseDialect.PostgreSql);
        Assert.NotEmpty(script.Diagnostics);
        var s = Assert.IsType<SelectStatement>(Assert.Single(script.Statements));
        var from = Assert.IsType<TableRef>(s.From);
        Assert.Equal("", from.Name);
        AssertInside(s, sql.Length);
    }

    [Fact]
    public void Parse_WhereAfterMissingTable_IsKept() =>
        Assert.NotNull(Assert.IsType<SelectStatement>(
            Assert.Single(SqlParser.Parse("select * from where a = 1", DatabaseDialect.PostgreSql).Statements)).Where);

    [Theory]
    [InlineData("select a, from t")]
    [InlineData("select (select from t")]
    [InlineData("select * from t join")]
    [InlineData("with x as (select 1")]
    [InlineData("select * from (select 1")]
    [InlineData("select ) from t")]
    [InlineData("(select 1")]
    public void Parse_Garbage_NeverThrowsAndStaysInside(string sql)
    {
        var script = SqlParser.Parse(sql, DatabaseDialect.PostgreSql);
        Assert.NotEmpty(script.Diagnostics);
        script.Statements.ToList().ForEach(s => AssertInside(s, sql.Length));
    }

    [Fact]
    public void Parse_MultipleStatements_UseAbsolutePositions()
    {
        var sql = "select 1; select a from t; update t set a = 1";
        var script = SqlParser.Parse(sql, DatabaseDialect.PostgreSql);
        Assert.Equal(3, script.Statements.Count);
        Assert.Equal(sql.IndexOf("select a", StringComparison.Ordinal), script.Statements[1].Start);
        Assert.IsType<UnknownStatement>(script.Statements[2]);
        Assert.Empty(script.Diagnostics);
    }
}
