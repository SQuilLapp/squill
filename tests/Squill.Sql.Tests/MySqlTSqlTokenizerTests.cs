using Squill.Core;
using Squill.Sql.Lexing;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class MySqlTSqlTokenizerTests
{
    private static (TokenKind Kind, string Text)[] Lex(DatabaseDialect dialect, string sql) =>
        [.. SqlTokenizers.For(dialect).Tokenize(sql).Select(t => (t.Kind, t.Text(sql)))];

    [Fact]
    public void For_MySqlAndSqlServer_ReturnDedicatedTokenizers()
    {
        Assert.IsType<MySqlTokenizer>(SqlTokenizers.For(DatabaseDialect.MySql));
        Assert.IsType<TSqlTokenizer>(SqlTokenizers.For(DatabaseDialect.SqlServer));
    }

    [Theory]
    [InlineData("`a``b`", TokenKind.QuotedIdentifier)]
    [InlineData("`open", TokenKind.QuotedIdentifier)]
    [InlineData("# comment", TokenKind.LineComment)]
    [InlineData("-- comment", TokenKind.LineComment)]
    [InlineData("--", TokenKind.LineComment)]
    [InlineData("\"a\\\"b\"", TokenKind.StringLiteral)]
    [InlineData("'it\\'s'", TokenKind.StringLiteral)]
    [InlineData("'it''s'", TokenKind.StringLiteral)]
    [InlineData("\"open", TokenKind.StringLiteral)]
    [InlineData("'abc\\", TokenKind.StringLiteral)]
    [InlineData("?", TokenKind.Parameter)]
    [InlineData("@name", TokenKind.Parameter)]
    [InlineData("@@global", TokenKind.Identifier)]
    [InlineData("/*! SET x=1 */", TokenKind.BlockComment)]
    [InlineData("/* c */", TokenKind.BlockComment)]
    [InlineData("auto_increment", TokenKind.Keyword)]
    [InlineData("STRAIGHT_JOIN", TokenKind.Keyword)]
    [InlineData("unsigned", TokenKind.Keyword)]
    public void MySql_SingleToken_CoversAllText(string sql, TokenKind kind) =>
        Assert.Equal([(kind, sql)], Lex(DatabaseDialect.MySql, sql));

    [Fact]
    public void MySql_DoubleDashWithoutSpace_IsTwoOperators() =>
        Assert.Equal(
            [(TokenKind.NumberLiteral, "1"), (TokenKind.Operator, "-"), (TokenKind.Operator, "-"), (TokenKind.NumberLiteral, "2")],
            Lex(DatabaseDialect.MySql, "1--2"));

    [Fact]
    public void MySql_CommentStopsAtLineEnd() =>
        Assert.Equal(
            [(TokenKind.LineComment, "# c"), (TokenKind.Whitespace, "\n"), (TokenKind.Keyword, "select")],
            Lex(DatabaseDialect.MySql, "# c\nselect"));

    [Theory]
    [InlineData("@")]
    [InlineData("@@")]
    public void MySql_BareAt_IsUnknown(string sql) =>
        Assert.All(Lex(DatabaseDialect.MySql, sql), t => Assert.Equal(TokenKind.Unknown, t.Kind));

    [Theory]
    [InlineData("[my col]", TokenKind.QuotedIdentifier)]
    [InlineData("[a]]b]", TokenKind.QuotedIdentifier)]
    [InlineData("[open", TokenKind.QuotedIdentifier)]
    [InlineData("N'text'", TokenKind.StringLiteral)]
    [InlineData("n'it''s'", TokenKind.StringLiteral)]
    [InlineData("N'open", TokenKind.StringLiteral)]
    [InlineData("@name", TokenKind.Parameter)]
    [InlineData("@@rowcount", TokenKind.Identifier)]
    [InlineData("#temp", TokenKind.Identifier)]
    [InlineData("##global", TokenKind.Identifier)]
    [InlineData("/* a /* b */ c */", TokenKind.BlockComment)]
    [InlineData("/* a /* b */", TokenKind.BlockComment)]
    [InlineData("-- c", TokenKind.LineComment)]
    [InlineData("GO", TokenKind.Identifier)]
    [InlineData("top", TokenKind.Keyword)]
    [InlineData("NOLOCK", TokenKind.Keyword)]
    [InlineData("identity", TokenKind.Keyword)]
    [InlineData("OUTPUT", TokenKind.Keyword)]
    [InlineData("merge", TokenKind.Keyword)]
    public void TSql_SingleToken_CoversAllText(string sql, TokenKind kind) =>
        Assert.Equal([(kind, sql)], Lex(DatabaseDialect.SqlServer, sql));

    [Theory]
    [InlineData("@")]
    [InlineData("#")]
    public void TSql_BareSigil_IsUnknown(string sql) =>
        Assert.Equal([(TokenKind.Unknown, sql)], Lex(DatabaseDialect.SqlServer, sql));

    [Fact]
    public void TSql_IdentifierEndingInN_IsNotUnicodeString() =>
        Assert.Equal(
            [(TokenKind.Identifier, "an"), (TokenKind.StringLiteral, "'x'")], Lex(DatabaseDialect.SqlServer, "an'x'"));

    [Theory]
    [InlineData(DatabaseDialect.MySql)]
    [InlineData(DatabaseDialect.SqlServer)]
    public void Tokenize_HandWrittenAndFuzz_StaysContiguous(DatabaseDialect dialect)
    {
        var tokenizer = SqlTokenizers.For(dialect);
        var parts = new[]
        {
            "SELECT", " ", "'", "''", "\"", "--", "/*", "/*!", "*/", "1", ".", "N'", "\\", "@", "@@", "@x", "#", "##",
            "[", "]", "]]", "`", "?", "(", ")", ",", ";", "x", "\n", "é", "$",
        };
        var rng = new Random(11);
        for (var i = 0; i < 200; i++)
        {
            AssertContiguous(tokenizer, string.Concat(Enumerable.Range(0, rng.Next(1, 14)).Select(_ => parts[rng.Next(parts.Length)])));
        }

        rng = new Random(43);
        for (var i = 0; i < 1000; i++)
        {
            var chars = new char[rng.Next(0, 201)];
            for (var j = 0; j < chars.Length; j++)
            {
                chars[j] = (char)rng.Next(0, 0x250);
            }

            AssertContiguous(tokenizer, new string(chars));
        }
    }

    private static void AssertContiguous(ISqlTokenizer tokenizer, string sql)
    {
        var pos = 0;
        foreach (var token in tokenizer.Tokenize(sql))
        {
            Assert.Equal(pos, token.Start);
            Assert.True(token.Length > 0);
            pos = token.End;
        }

        Assert.Equal(sql.Length, pos);
    }
}
