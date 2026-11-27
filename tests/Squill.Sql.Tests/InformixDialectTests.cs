using Squill.Core;
using Squill.Sql.Completion;
using Squill.Sql.Ddl;
using Squill.Sql.Editing;
using Squill.Sql.Functions;
using Squill.Sql.Lexing;
using Squill.Sql.Statements;
using Squill.Sql.Syntax;
using Squill.Sql.Transactions;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class InformixDialectTests
{
    [Fact]
    public void Tokenizer_NamedParameter_IsAParameterToken()
    {
        var tokens = SqlTokenizers.For(DatabaseDialect.Informix).Tokenize("select :id, ':no' from t");

        Assert.Single(tokens, t => t.Kind == TokenKind.Parameter);
    }

    [Fact]
    public void Tokenizer_BraceComment_IsOneCommentToken()
    {
        var tokens = SqlTokenizers.For(DatabaseDialect.Informix).Tokenize("select { :a } 1");

        Assert.Single(tokens, t => t.Kind == TokenKind.BlockComment);
        Assert.DoesNotContain(tokens, t => t.Kind == TokenKind.Parameter);
    }

    [Fact]
    public void TransactionSql_UsesTheStandardStatements()
    {
        Assert.Equal("BEGIN", TransactionSql.Begin(DatabaseDialect.Informix));
        Assert.Equal("COMMIT", TransactionSql.Commit(DatabaseDialect.Informix));
        Assert.Equal("ROLLBACK", TransactionSql.Rollback(DatabaseDialect.Informix));
    }

    [Fact]
    public void Ddl_DropWorks_DesignerIsNotAvailable()
    {
        var generator = DdlGenerators.For(DatabaseDialect.Informix);

        Assert.False(DdlGenerators.CanDesign(DatabaseDialect.Informix));
        Assert.Equal(["DROP TABLE \"t1\""], generator.DropTable(null, "t1"));
        Assert.Throws<NotSupportedException>(() => generator.CreateTable(null!));
    }

    private const DatabaseDialect Informix = DatabaseDialect.Informix;

    private static string[] Split(string sql) =>
        [.. SqlStatementSplitter.Split(sql, Informix).Select(s => sql.Substring(s.Start, s.Length))];

    [Theory]
    [InlineData("first")]
    [InlineData("skip")]
    [InlineData("matches")]
    [InlineData("today")]
    [InlineData("extend")]
    [InlineData("serial8")]
    [InlineData("lvarchar")]
    [InlineData("datetime")]
    public void Tokenizer_InformixWord_IsAKeyword(string word) =>
        Assert.Equal(TokenKind.Keyword, SqlTokenizers.For(Informix).Tokenize(word)[0].Kind);

    [Theory]
    [InlineData("\"My Col\"", TokenKind.QuotedIdentifier)]
    [InlineData("?", TokenKind.Parameter)]
    [InlineData("-- c", TokenKind.LineComment)]
    public void Tokenizer_SingleToken_CoversAllText(string sql, TokenKind kind)
    {
        var tokens = SqlTokenizers.For(Informix).Tokenize(sql);
        Assert.Equal(kind, Assert.Single(tokens).Kind);
    }

    [Fact]
    public void Split_Procedure_IsOneStatementUpToEndProcedure()
    {
        const string Proc = """
            create procedure p(a integer) returning integer;
             let a = a + 1;
             if a > 1 then
              return a;
             end if;
             return 0;
            end procedure;
            """;
        Assert.Equal([Proc, "select 1 from systables"], Split(Proc + " select 1 from systables"));
    }

    [Fact]
    public void Split_DbaFunction_IsOneStatement()
    {
        const string Func = "create dba function f() returning int; return 1; end function;";
        Assert.Equal([Func, "select 2"], Split(Func + " select 2"));
    }

    [Fact]
    public void Split_CreateTableNamedLikeAProcedure_EndsAtSemicolon() =>
        Assert.Equal(["create table procedure_t (a int);", "select 1"], Split("create table procedure_t (a int); select 1"));

    [Theory]
    [InlineData("select first 5 * from customers")]
    [InlineData("select skip 2 first 5 * from customers")]
    [InlineData("select first ? name from customers")]
    public void Parse_FirstAndSkip_HaveNoDiagnostic(string sql)
    {
        var script = SqlParser.Parse(sql, Informix);
        Assert.Empty(script.Diagnostics);
        Assert.NotNull(Assert.IsType<SelectStatement>(Assert.Single(script.Statements)).Top);
    }

    [Fact]
    public void Parse_Skip_IsTheOffset() =>
        Assert.NotNull(Assert.IsType<SelectStatement>(Assert.Single(SqlParser.Parse("select skip 2 first 5 * from t", Informix).Statements)).Offset);

    [Fact]
    public void Parse_ColumnNamedFirst_IsStillAColumn()
    {
        var script = SqlParser.Parse("select first from t", Informix);
        Assert.Empty(script.Diagnostics);
        Assert.Null(Assert.IsType<SelectStatement>(Assert.Single(script.Statements)).Top);
    }

    [Fact]
    public void Analyze_SelectFirst_IsEditable()
    {
        var table = new TableInfo(null, "customers", TableKind.Table, [new ColumnDefinition("id", "integer", false, true, null), new ColumnDefinition("name", "varchar", true, false, null)], [], []);
        var result = EditableSourceAnalyzer.Analyze("select first 5 * from customers", Informix, new SchemaSnapshot(null, [table]), [new ColumnInfo("id", "integer", typeof(int), false, 0), new ColumnInfo("name", "varchar", typeof(string), true, 1)]);

        Assert.Null(result.Reason);
        Assert.NotNull(result.Target);
    }

    [Theory]
    [InlineData("orders", "orders")]
    [InlineData("Orders", "\"Orders\"")]
    [InlineData("first", "\"first\"")]
    [InlineData("my col", "\"my col\"")]
    public void Quote_FoldsToLowerCase(string name, string expected) =>
        Assert.Equal(expected, IdentifierQuoting.Quote(name, Informix));

    private static readonly string[] ExpectedFunctions = ["NVL", "DECODE", "TODAY", "MDY", "EXTEND"];

    private static readonly string[] ExpectedTypes = ["SERIAL8", "BIGSERIAL", "INT8", "LVARCHAR(2048)", "MONEY(16,2)", "BYTE", "TEXT", "BLOB", "CLOB", "BOOLEAN"];

    [Fact]
    public void Functions_HaveTheInformixOnes() =>
        Assert.All(ExpectedFunctions, n => Assert.Contains(FunctionCatalog.For(Informix), f => f.Name == n));

    [Fact]
    public void DataTypes_HaveTheInformixOnes() =>
        Assert.All(ExpectedTypes, t => Assert.Contains(t, DataTypeCatalog.For(Informix)));
}
