using Squill.Core;
using Squill.Sql.Lexing;
using Squill.Sql.Syntax;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class ExpressionParserTests
{
    private static Expression Parse(string sql, out IReadOnlyList<ParseDiagnostic> diagnostics,
        DatabaseDialect dialect = DatabaseDialect.PostgreSql) =>
        ExpressionParser.ParseText(sql, dialect, out diagnostics);

    private static Expression Ok(string sql, DatabaseDialect dialect = DatabaseDialect.PostgreSql)
    {
        var expression = Parse(sql, out var diagnostics, dialect);
        Assert.Empty(diagnostics);
        AssertInside(expression, sql);
        return expression;
    }

    private static IEnumerable<SyntaxNode> Children(SyntaxNode node) => node switch
    {
        FunctionCall f => f.Args,
        BinaryExpr b => [b.Left, b.Right],
        UnaryExpr u => [u.Operand],
        CaseExpr c => [.. (c.Operand is null ? [] : new[] { c.Operand }), .. c.Whens, .. (c.Else is null ? [] : new[] { c.Else })],
        WhenClause w => [w.Condition, w.Result],
        CastExpr c => [c.Operand],
        ParenExpr p => [p.Inner],
        ListExpr l => l.Items,
        SubqueryExpr s => [s.Query],
        _ => [],
    };

    private static void AssertInside(SyntaxNode node, string sql, int low = 0, int high = -1)
    {
        high = high < 0 ? sql.Length : high;
        Assert.InRange(node.Start, low, high);
        Assert.InRange(node.End, node.Start, high);
        foreach (var child in Children(node))
        {
            AssertInside(child, sql, node.Start, node.End);
        }
    }

    private static string Show(Expression e) => e switch
    {
        ColumnRef c => c.Qualifier is null ? c.Name : $"{c.Qualifier}.{c.Name}",
        LiteralExpr l => l.Kind.ToString(),
        BinaryExpr b => $"({Show(b.Left)} {b.Op} {Show(b.Right)})",
        UnaryExpr u => $"({u.Op} {Show(u.Operand)})",
        ListExpr l => "[" + string.Join(", ", l.Items.Select(Show)) + "]",
        ParenExpr p => $"<{Show(p.Inner)}>",
        _ => e.GetType().Name,
    };

    [Theory]
    [InlineData("1 + 2 * 3", "(Number + (Number * Number))")]
    [InlineData("1 * 2 + 3", "((Number * Number) + Number)")]
    [InlineData("a OR b AND c", "(a OR (b AND c))")]
    [InlineData("a AND b OR c", "((a AND b) OR c)")]
    [InlineData("NOT a = b", "(NOT (a = b))")]
    [InlineData("NOT a AND b", "((NOT a) AND b)")]
    [InlineData("a = 1 AND b <> 2", "((a = Number) AND (b <> Number))")]
    [InlineData("-a * b", "((- a) * b)")]
    [InlineData("a || b = c", "((a || b) = c)")]
    [InlineData("a - b - c", "((a - b) - c)")]
    [InlineData("(a + b) * c", "(<(a + b)> * c)")]
    [InlineData("a IS NULL", "(a IS Null)")]
    [InlineData("a IS NOT NULL", "(a IS NOT Null)")]
    [InlineData("a IS DISTINCT FROM b", "(a IS DISTINCT FROM b)")]
    [InlineData("a IN (1, 2)", "(a IN [Number, Number])")]
    [InlineData("a NOT IN (1)", "(a NOT IN [Number])")]
    [InlineData("a BETWEEN 1 AND 2 AND b", "((a BETWEEN (Number AND Number)) AND b)")]
    [InlineData("a NOT BETWEEN 1 AND 2", "(a NOT BETWEEN (Number AND Number))")]
    [InlineData("a LIKE 'x%'", "(a LIKE Text)")]
    [InlineData("a ILIKE 'x%'", "(a ILIKE Text)")]
    public void Parse_Operators_RespectPrecedence(string sql, string expected) =>
        Assert.Equal(expected, Show(Ok(sql)));

    [Fact]
    public void Parse_DottedName_SplitsQualifierAndName()
    {
        var column = Assert.IsType<ColumnRef>(Ok("a.b.c"));
        Assert.Equal(("a.b", "c"), (column.Qualifier, column.Name));
        Assert.Equal((0, 5), (column.Start, column.Length));
    }

    [Fact]
    public void Parse_QuotedNames_AreUnquoted()
    {
        var pg = Assert.IsType<ColumnRef>(Ok("\"S\".\"a\"\"b\""));
        Assert.Equal(("S", "a\"b"), (pg.Qualifier, pg.Name));
        var tsql = Assert.IsType<ColumnRef>(Ok("[dbo].[t]", DatabaseDialect.SqlServer));
        Assert.Equal(("dbo", "t"), (tsql.Qualifier, tsql.Name));
    }

    [Fact]
    public void Parse_Star_KeepsQualifier()
    {
        Assert.Equal(new StarExpr(null, 0, 1), Ok("*"));
        Assert.Equal(new StarExpr("t", 0, 3), Ok("t.*"));
    }

    [Fact]
    public void Parse_Parameters_KeepPrefix()
    {
        Assert.Equal("@p", Assert.IsType<ParameterExpr>(Ok("@p", DatabaseDialect.SqlServer)).Name);
        Assert.Equal("$1", Assert.IsType<ParameterExpr>(Ok("$1")).Name);
    }

    [Fact]
    public void Parse_FunctionCalls_ReadArgumentsAndDistinct()
    {
        var count = Assert.IsType<FunctionCall>(Ok("count(DISTINCT a)"));
        Assert.True(count.Distinct);
        Assert.Equal("count", count.Name);
        Assert.Single(count.Args);

        Assert.IsType<StarExpr>(Assert.Single(Assert.IsType<FunctionCall>(Ok("count(*)")).Args));
        Assert.Empty(Assert.IsType<FunctionCall>(Ok("now()")).Args);

        var nested = Assert.IsType<FunctionCall>(Ok("pg_catalog.f(1, g(2), a)"));
        Assert.Equal("pg_catalog.f", nested.Name);
        Assert.Equal(3, nested.Args.Count);
        Assert.IsType<FunctionCall>(nested.Args[1]);
    }

    [Fact]
    public void Parse_LeftWithParenthesis_IsFunctionNotJoin()
    {
        Assert.Equal("LEFT", Assert.IsType<FunctionCall>(Ok("LEFT(a, 2)")).Name);
    }

    [Fact]
    public void Parse_SearchedCase_ReadsBranchesAndElse()
    {
        var node = Assert.IsType<CaseExpr>(Ok("CASE WHEN a = 1 THEN 'x' WHEN a = 2 THEN 'y' ELSE 'z' END"));
        Assert.Null(node.Operand);
        Assert.Equal(2, node.Whens.Count);
        Assert.NotNull(node.Else);
    }

    [Fact]
    public void Parse_SimpleCase_ReadsOperand()
    {
        var node = Assert.IsType<CaseExpr>(Ok("CASE a WHEN 1 THEN 2 END"));
        Assert.NotNull(node.Operand);
        Assert.Null(node.Else);
    }

    [Theory]
    [InlineData("CAST(a AS int)", "int")]
    [InlineData("CAST(a AS double precision)", "double precision")]
    [InlineData("CAST(a AS varchar(10))", "varchar(10)")]
    [InlineData("a::int", "int")]
    [InlineData("a::numeric(10, 2)", "numeric(10, 2)")]
    public void Parse_Cast_ReadsTypeName(string sql, string type) =>
        Assert.Equal(type, Assert.IsType<CastExpr>(Ok(sql)).TypeName);

    [Fact]
    public void Parse_CastBindsTighterThanArithmetic() =>
        Assert.Equal("(a + CastExpr)", Show(Ok("a + b::int")));

    [Fact]
    public void Parse_Subquery_IsOpaqueAndBalanced()
    {
        var exists = Assert.IsType<UnaryExpr>(Ok("EXISTS (SELECT f(1) FROM t)"));
        Assert.IsType<SubqueryExpr>(exists.Operand);

        var inQuery = Assert.IsType<BinaryExpr>(Ok("a IN (SELECT b FROM t) AND c"));
        Assert.Equal("AND", inQuery.Op);
        Assert.IsType<SubqueryExpr>(Assert.IsType<BinaryExpr>(inQuery.Left).Right);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a +")]
    [InlineData("f(1,")]
    [InlineData("f(")]
    [InlineData("a.")]
    [InlineData("a.b.")]
    [InlineData("(a")]
    [InlineData("()")]
    [InlineData("CASE")]
    [InlineData("CASE WHEN a")]
    [InlineData("CASE WHEN a THEN")]
    [InlineData("a BETWEEN 1")]
    [InlineData("a IN (")]
    [InlineData("a IN 1")]
    [InlineData("a IS")]
    [InlineData("CAST(a AS")]
    [InlineData("CAST(a")]
    [InlineData("a::")]
    [InlineData("NOT")]
    [InlineData(")")]
    [InlineData("a b")]
    [InlineData("# 1")]
    [InlineData("a = = b")]
    [InlineData("EXISTS")]
    [InlineData("(SELECT 1")]
    public void Parse_IncompleteInput_ReportsDiagnosticsAndStaysInsideInput(string sql)
    {
        var expression = Parse(sql, out var diagnostics);
        Assert.NotEmpty(diagnostics);
        AssertInside(expression, sql);
        Assert.All(diagnostics, d => Assert.InRange(d.Start + d.Length, 0, sql.Length));
    }

    [Fact]
    public void Parse_TrailingDot_YieldsEmptyName()
    {
        var column = Assert.IsType<ColumnRef>(Parse("a.", out var diagnostics));
        Assert.Equal(("a", ""), (column.Qualifier, column.Name));
        Assert.Single(diagnostics);
    }

    [Fact]
    public void Parse_Incomplete_ContainsErrorNodeAtEnd()
    {
        var binary = Assert.IsType<BinaryExpr>(Parse("a + ", out _));
        var error = Assert.IsType<ErrorNode>(binary.Right);
        Assert.Equal(0, error.Length);
    }

    [Fact]
    public void ParseExpression_ClauseKeyword_StopsWithoutConsuming()
    {
        var core = new SqlParserCore("a + FROM t", DatabaseDialect.PostgreSql);
        var expression = new ExpressionParser(core).ParseExpression();
        Assert.IsType<BinaryExpr>(expression);
        Assert.Equal(TokenKind.Keyword, core.Peek().Kind);
        Assert.Equal("FROM", core.TextOf(core.Peek()));
    }

    [Theory]
    [InlineData("a, b")]
    [InlineData("a) b")]
    [InlineData("a; b")]
    public void ParseExpression_Separator_StopsWithoutConsuming(string sql)
    {
        var core = new SqlParserCore(sql, DatabaseDialect.PostgreSql);
        Assert.IsType<ColumnRef>(new ExpressionParser(core).ParseExpression());
        Assert.Equal(TokenKind.Punctuation, core.Peek().Kind);
    }

    [Fact]
    public void ParseExpression_TrailingDotBeforeClause_DoesNotSwallowKeyword()
    {
        var core = new SqlParserCore("t. FROM x", DatabaseDialect.PostgreSql);
        var column = Assert.IsType<ColumnRef>(new ExpressionParser(core).ParseExpression());
        Assert.Equal("", column.Name);
        Assert.Equal("FROM", core.TextOf(core.Peek()));
    }

    [Fact]
    public void Parse_EveryPrefixOfAComplexExpression_NeverThrows()
    {
        const string sql = "CASE WHEN a.x IN (1, 2) AND NOT b BETWEEN 1 AND f(DISTINCT c, 'it''s') THEN CAST(d AS varchar(5)) "
            + "ELSE -e::int * (g + @p) END IS NOT NULL OR EXISTS (SELECT 1) AND t.* <> \"q\"";
        for (var length = 0; length <= sql.Length; length++)
        {
            var prefix = sql[..length];
            AssertInside(Parse(prefix, out _), prefix);
        }
    }
}
