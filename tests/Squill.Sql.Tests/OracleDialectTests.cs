using Squill.Core;
using Squill.Sql.Completion;
using Squill.Sql.Ddl;
using Squill.Sql.Functions;
using Squill.Sql.Lexing;
using Squill.Sql.Statements;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class OracleDialectTests
{
    private const DatabaseDialect Oracle = DatabaseDialect.Oracle;

    private static (TokenKind Kind, string Text)[] Lex(string sql) =>
        [.. SqlTokenizers.For(Oracle).Tokenize(sql).Select(t => (t.Kind, t.Text(sql)))];

    private static string[] Split(string sql) =>
        [.. SqlStatementSplitter.Split(sql, Oracle).Select(s => sql.Substring(s.Start, s.Length))];

    [Fact]
    public void For_Oracle_ReturnsTheDedicatedTokenizer() =>
        Assert.IsType<OracleTokenizer>(SqlTokenizers.For(Oracle));

    [Theory]
    [InlineData("q'[it's]'", TokenKind.StringLiteral)]
    [InlineData("Q'!a;b!'", TokenKind.StringLiteral)]
    [InlineData("q'{a}'", TokenKind.StringLiteral)]
    [InlineData("q'(a)'", TokenKind.StringLiteral)]
    [InlineData("q'<a>'", TokenKind.StringLiteral)]
    [InlineData("nq'[x]'", TokenKind.StringLiteral)]
    [InlineData("q'[open", TokenKind.StringLiteral)]
    [InlineData("N'text'", TokenKind.StringLiteral)]
    [InlineData("\"My Col\"", TokenKind.QuotedIdentifier)]
    [InlineData(":name", TokenKind.Parameter)]
    [InlineData(":1", TokenKind.Parameter)]
    [InlineData("-- c", TokenKind.LineComment)]
    [InlineData("/* c */", TokenKind.BlockComment)]
    [InlineData("varchar2", TokenKind.Keyword)]
    [InlineData("sysdate", TokenKind.Keyword)]
    [InlineData("a$b#", TokenKind.Identifier)]
    [InlineData(":=", TokenKind.Operator)]
    public void Oracle_SingleToken_CoversAllText(string sql, TokenKind kind) =>
        Assert.Equal([(kind, sql)], Lex(sql));

    [Fact]
    public void Oracle_AlternativeQuoting_KeepsQuotesAndSemicolonsInsideOneString() =>
        Assert.Equal(
            [(TokenKind.Keyword, "select"), (TokenKind.Whitespace, " "), (TokenKind.StringLiteral, "q'[a ' ; ]'"), (TokenKind.Punctuation, ";")],
            Lex("select q'[a ' ; ]';"));

    [Fact]
    public void Oracle_IdentifierEndingInQ_IsNotAlternativeQuoting() =>
        Assert.Equal([(TokenKind.Identifier, "faq"), (TokenKind.StringLiteral, "'x'")], Lex("faq'x'"));

    [Fact]
    public void Oracle_Backtick_IsUnknown() =>
        Assert.Equal([(TokenKind.Unknown, "`")], Lex("`"));

    [Fact]
    public void Split_PlainStatements_EndAtSemicolon() =>
        Assert.Equal(["select 1 from dual;", "select 2 from dual;"], Split("select 1 from dual;\nselect 2 from dual;"));

    [Fact]
    public void Split_AnonymousBlock_EndsAtSlashLine()
    {
        const string Block = "begin\n  null;\n  null;\nend;";
        Assert.Equal([Block, "select 1 from dual"], Split(Block + "\n/\nselect 1 from dual\n/\n"));
    }

    [Fact]
    public void Split_DeclareBlock_KeepsInnerSemicolons()
    {
        const string Block = "declare\n  v number;\nbegin\n  v := 1;\nend;";
        Assert.Equal([Block], Split(Block + "\n/"));
    }

    [Theory]
    [InlineData("create or replace procedure p is begin null; end;")]
    [InlineData("CREATE FUNCTION f RETURN NUMBER IS BEGIN RETURN 1; END;")]
    [InlineData("create or replace editionable package pkg as procedure q; end pkg;")]
    [InlineData("create or replace trigger t before insert on x for each row begin null; end;")]
    [InlineData("create or replace type t as object (a number);")]
    public void Split_StoredPlsqlObject_EndsAtSlashLine(string sql) =>
        Assert.Equal([sql, "select 1 from dual;"], Split(sql + "\n/\nselect 1 from dual;"));

    [Fact]
    public void Split_CreateTable_EndsAtSemicolonEvenWhenANameLooksLikeAKeyword() =>
        Assert.Equal(["create table function_t (a number);", "select 1 from dual;"], Split("create table function_t (a number);\nselect 1 from dual;"));

    [Fact]
    public void Split_SlashInsideAnExpression_IsNotATerminator() =>
        Assert.Equal(["select 4 / 2 from dual"], Split("select 4 / 2 from dual"));

    [Fact]
    public void Split_LoneSlashWithoutStatement_IsIgnored() =>
        Assert.Empty(Split("/\n  /  \n"));

    [Fact]
    public void Split_OtherDialects_IgnoreTheSlashLineRule() =>
        Assert.Single(SqlStatementSplitter.Split("select 1\n/\n2", DatabaseDialect.PostgreSql));

    [Theory]
    [InlineData("EMP", "EMP")]
    [InlineData("EMP_NO$1", "EMP_NO$1")]
    [InlineData("emp", "\"emp\"")]
    [InlineData("Emp", "\"Emp\"")]
    [InlineData("1ST", "\"1ST\"")]
    [InlineData("MY COL", "\"MY COL\"")]
    [InlineData("ORDER", "\"ORDER\"")]
    [InlineData("SYSDATE", "\"SYSDATE\"")]
    [InlineData("A\"B", "\"A\"\"B\"")]
    [InlineData("", "\"\"")]
    public void Quote_FollowsUpperCaseFolding(string name, string expected) =>
        Assert.Equal(expected, IdentifierQuoting.Quote(name, Oracle));

    [Fact]
    public void DataTypes_ListTheOracleTypes()
    {
        var types = DataTypeCatalog.For(Oracle);
        foreach (var expected in new[] { "NUMBER(18,2)", "VARCHAR2(255)", "NVARCHAR2(255)", "CHAR(1)", "CLOB", "BLOB", "DATE", "RAW(16)", "BINARY_FLOAT", "BINARY_DOUBLE", "TIMESTAMP WITH TIME ZONE", "TIMESTAMP WITH LOCAL TIME ZONE", "JSON" })
            Assert.Contains(expected, types);
    }

    [Fact]
    public void Functions_HaveTheOracleOnesWithSignatures()
    {
        var functions = FunctionCatalog.For(Oracle);
        foreach (var name in new[] { "NVL", "NVL2", "DECODE", "TO_CHAR", "TO_DATE", "SYSDATE", "SYSTIMESTAMP", "LISTAGG", "COUNT" })
            Assert.Contains(functions, f => f.Name == name);
        Assert.Equal(["expr", "fallback"], functions.Single(f => f.Name == "NVL").Parameters);
        Assert.DoesNotContain(functions, f => f.Name is "ISNULL" or "GETDATE");
    }

    [Fact]
    public void KeywordCompletion_UsesFetchFirstAndOracleStatements()
    {
        var items = new KeywordCompletionProvider().GetCompletions(new SqlRequest("select a from t ", 16, Oracle, null));
        Assert.Contains(items, i => i.Label == "FETCH FIRST");
        Assert.DoesNotContain(items, i => i.Label == "LIMIT");

        var starts = new KeywordCompletionProvider().GetCompletions(new SqlRequest("", 0, Oracle, null));
        Assert.Contains(starts, i => i.Label == "MERGE");
        Assert.DoesNotContain(starts, i => i.Label is "PRAGMA" or "VACUUM");
    }
}
