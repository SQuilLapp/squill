using Squill.Core;
using Squill.Sql.Analysis;
using Squill.Sql.Completion;
using Squill.Sql.Lexing;
using Squill.Sql.Syntax;
using Xunit;

namespace Squill.Sql.Tests;

/// <summary>Constructs the statement corpus (P11-27) showed the parser did not accept.</summary>
public sealed class DialectGapTests
{
    private static readonly SchemaSnapshot Schema = new(null,
    [
        new TableInfo(null, "t", TableKind.Table, [new("a", "int", true, false, null), new("b", "int", true, false, null)], [], []),
    ]);

    private static SqlScript Parse(string sql, DatabaseDialect dialect)
    {
        var script = SqlParser.Parse(sql, dialect);
        Assert.Empty(script.Diagnostics);
        return script;
    }

    private static FunctionCall FirstCall(SqlScript script) =>
        Assert.IsType<FunctionCall>(Assert.IsType<SelectStatement>(Assert.Single(script.Statements)).Items[0].Value);

    [Fact]
    public void WindowFunction_KeepsPartitionAndOrderExpressions()
    {
        var call = FirstCall(Parse("select sum(a) over (partition by b order by a desc rows between unbounded preceding and current row) from t", DatabaseDialect.PostgreSql));
        Assert.Equal(2, call.Extra!.Count);
    }

    [Fact]
    public void WindowFunction_UnknownColumnInPartitionIsReported()
    {
        var request = new SqlRequest("select row_number() over (partition by nope) from t", 0, DatabaseDialect.PostgreSql, Schema);
        Assert.Contains(SqlDiagnosticsProvider.GetDiagnostics(request), d => d.Code == "SQL1002");
    }

    [Fact]
    public void FilterClause_IsPartOfTheCall()
    {
        var call = FirstCall(Parse("select count(*) filter (where a > 1) from t", DatabaseDialect.PostgreSql));
        Assert.Single(call.Extra!);
    }

    [Theory]
    [InlineData("select dateadd(day, 1, a) from t", DatabaseDialect.SqlServer)]
    [InlineData("select extract(year from a) from t", DatabaseDialect.PostgreSql)]
    [InlineData("select a from t where a > date_add(a, interval 1 day)", DatabaseDialect.MySql)]
    [InlineData("select group_concat(a separator ',') from t", DatabaseDialect.MySql)]
    public void BareWordArguments_AreNotColumns(string sql, DatabaseDialect dialect)
    {
        Parse(sql, dialect);
        Assert.Empty(SqlDiagnosticsProvider.GetDiagnostics(new SqlRequest(sql, 0, dialect, Schema)));
    }

    [Theory]
    [InlineData("select a ->> 'k', a -> 'k', a @> b, a ~ 'x', a !~* 'x' from t", DatabaseDialect.PostgreSql)]
    [InlineData("select a <=> b, a regexp 'x', a not rlike 'x', @v := 1 from t", DatabaseDialect.MySql)]
    [InlineData("select a glob 'x', a not glob 'y', x'41', 0x1f from t", DatabaseDialect.Sqlite)]
    [InlineData("select array[1, 2][1], (array[a, b])[2], a::int[] from t", DatabaseDialect.PostgreSql)]
    [InlineData("select a from t order by a collate \"C\"", DatabaseDialect.PostgreSql)]
    public void Operators_AndLiterals_Parse(string sql, DatabaseDialect dialect) => Parse(sql, dialect);

    [Theory]
    [InlineData("select * from t, lateral (select 1) x", DatabaseDialect.PostgreSql)]
    [InlineData("select * from t cross apply (select 1 as k) x outer apply (select 2 as k) y", DatabaseDialect.SqlServer)]
    [InlineData("select * from t with (nolock) where a = 1", DatabaseDialect.SqlServer)]
    [InlineData("select * from t x with (updlock) join t y on y.a = x.a", DatabaseDialect.SqlServer)]
    [InlineData("select * from t indexed by ix where a = 1", DatabaseDialect.Sqlite)]
    [InlineData("select * from t not indexed", DatabaseDialect.Sqlite)]
    [InlineData("select * from t straight_join t u on u.a = t.a", DatabaseDialect.MySql)]
    [InlineData("select * from generate_series(1, 3) as g(i)", DatabaseDialect.PostgreSql)]
    [InlineData("select a, b into #copy from t", DatabaseDialect.SqlServer)]
    [InlineData("select * from t for update skip locked", DatabaseDialect.PostgreSql)]
    [InlineData("select * from t lock in share mode", DatabaseDialect.MySql)]
    [InlineData("with n(i) as (select 1) select * from n option (maxrecursion 5)", DatabaseDialect.SqlServer)]
    [InlineData("select sql_calc_found_rows * from t limit 1", DatabaseDialect.MySql)]
    [InlineData("select a, lag(a) over w from t window w as (order by a)", DatabaseDialect.PostgreSql)]
    [InlineData("declare @v table (i int); insert into @v values (1); select * from @v", DatabaseDialect.SqlServer)]
    public void TableSourcesAndClauses_Parse(string sql, DatabaseDialect dialect) => Parse(sql, dialect);

    [Theory]
    [InlineData("insert or replace into t (a) values (1)", DatabaseDialect.Sqlite)]
    [InlineData("update or ignore t set a = 1", DatabaseDialect.Sqlite)]
    [InlineData("insert ignore into t (a) values (1)", DatabaseDialect.MySql)]
    [InlineData("insert into t set a = 1, b = 2", DatabaseDialect.MySql)]
    [InlineData("update t x join t y on y.a = x.a set x.b = 1 where y.b = 2", DatabaseDialect.MySql)]
    [InlineData("update top (5) t set a = 1", DatabaseDialect.SqlServer)]
    public void Dml_Variants_Parse(string sql, DatabaseDialect dialect) => Parse(sql, dialect);

    [Fact]
    public void UpdateTarget_ThatIsAnAliasOfTheFrom_IsNotAnUnknownTable()
    {
        const string sql = "update x set x.b = 1 from t x join t y on y.a = x.a";
        Assert.Empty(SqlDiagnosticsProvider.GetDiagnostics(new SqlRequest(sql, 0, DatabaseDialect.SqlServer, Schema)));
    }

    [Fact]
    public void TableFunctionAndTemporaryTable_AreNotUnknownTables()
    {
        Assert.Empty(SqlDiagnosticsProvider.GetDiagnostics(new SqlRequest("select * from generate_series(1, 2) g", 0, DatabaseDialect.PostgreSql, Schema)));
        Assert.Empty(SqlDiagnosticsProvider.GetDiagnostics(new SqlRequest("select * from #tmp", 0, DatabaseDialect.SqlServer, Schema)));
    }

    [Theory]
    [InlineData("->>", DatabaseDialect.PostgreSql)]
    [InlineData("<=>", DatabaseDialect.MySql)]
    [InlineData("!~*", DatabaseDialect.PostgreSql)]
    [InlineData(":=", DatabaseDialect.MySql)]
    public void MultiCharacterOperator_IsOneToken(string op, DatabaseDialect dialect)
    {
        var token = Assert.Single(SqlTokenizers.For(dialect).Tokenize(op));
        Assert.Equal((TokenKind.Operator, op.Length), (token.Kind, token.Length));
    }

    [Theory]
    [InlineData("x'41'", DatabaseDialect.MySql, TokenKind.StringLiteral)]
    [InlineData("b'101'", DatabaseDialect.MySql, TokenKind.StringLiteral)]
    [InlineData("0x1F", DatabaseDialect.Sqlite, TokenKind.NumberLiteral)]
    [InlineData("$5.00", DatabaseDialect.SqlServer, TokenKind.NumberLiteral)]
    public void RadixAndMoneyLiterals_AreOneToken(string text, DatabaseDialect dialect, TokenKind kind)
    {
        var token = Assert.Single(SqlTokenizers.For(dialect).Tokenize(text));
        Assert.Equal((kind, text.Length), (token.Kind, token.Length));
    }
}
