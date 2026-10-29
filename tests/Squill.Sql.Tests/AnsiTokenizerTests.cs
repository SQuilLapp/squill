using Squill.Core;
using Squill.Sql.Keywords;
using Squill.Sql.Lexing;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class AnsiTokenizerTests
{
    private static readonly ISqlTokenizer Tokenizer = SqlTokenizers.For(DatabaseDialect.PostgreSql);

    private static (TokenKind Kind, string Text)[] Lex(string sql) =>
        [.. Tokenizer.Tokenize(sql).Select(t => (t.Kind, t.Text(sql)))];

    [Theory]
    [InlineData("   \t\r\n", TokenKind.Whitespace)]
    [InlineData("-- hi", TokenKind.LineComment)]
    [InlineData("/* a */", TokenKind.BlockComment)]
    [InlineData("select", TokenKind.Keyword)]
    [InlineData("SeLeCt", TokenKind.Keyword)]
    [InlineData("users", TokenKind.Identifier)]
    [InlineData("_a1", TokenKind.Identifier)]
    [InlineData("\"a\"\"b\"", TokenKind.QuotedIdentifier)]
    [InlineData("'it''s'", TokenKind.StringLiteral)]
    [InlineData("12", TokenKind.NumberLiteral)]
    [InlineData("1.5", TokenKind.NumberLiteral)]
    [InlineData(".5", TokenKind.NumberLiteral)]
    [InlineData("1e10", TokenKind.NumberLiteral)]
    [InlineData("1.5E-3", TokenKind.NumberLiteral)]
    [InlineData("<=", TokenKind.Operator)]
    [InlineData("<>", TokenKind.Operator)]
    [InlineData("!=", TokenKind.Operator)]
    [InlineData(">=", TokenKind.Operator)]
    [InlineData("||", TokenKind.Operator)]
    [InlineData("+", TokenKind.Operator)]
    [InlineData("(", TokenKind.Punctuation)]
    [InlineData(";", TokenKind.Punctuation)]
    [InlineData(".", TokenKind.Punctuation)]
    [InlineData("#", TokenKind.Unknown)]
    public void Tokenize_SingleToken_ReturnsKindCoveringAllText(string sql, TokenKind kind) =>
        Assert.Equal([(kind, sql)], Lex(sql));

    [Fact]
    public void Tokenize_LineComment_StopsAtNewline() =>
        Assert.Equal([(TokenKind.LineComment, "-- a"), (TokenKind.Whitespace, "\n"), (TokenKind.Identifier, "b")], Lex("-- a\nb"));

    [Theory]
    [InlineData("'abc", TokenKind.StringLiteral)]
    [InlineData("/* abc", TokenKind.BlockComment)]
    [InlineData("\"abc", TokenKind.QuotedIdentifier)]
    [InlineData("-- abc", TokenKind.LineComment)]
    public void Tokenize_Unterminated_RunsToEndOfText(string sql, TokenKind kind) =>
        Assert.Equal([(kind, sql)], Lex(sql));

    [Fact]
    public void Tokenize_ExponentWithoutDigits_LeavesIdentifier() =>
        Assert.Equal([(TokenKind.NumberLiteral, "1"), (TokenKind.Identifier, "e")], Lex("1e"));

    [Fact]
    public void Tokenize_QualifiedName_SplitsOnDot() =>
        Assert.Equal(
            [(TokenKind.Identifier, "a"), (TokenKind.Punctuation, "."), (TokenKind.Identifier, "b")], Lex("a.b"));

    [Fact]
    public void Tokenize_EmptyText_ReturnsNoTokens() => Assert.Empty(Tokenizer.Tokenize(""));

    [Fact]
    public void Keywords_AnyCase_Match()
    {
        Assert.Contains("where", (IReadOnlySet<string>)AnsiKeywords.Words);
        Assert.Contains("WHERE", (IReadOnlySet<string>)AnsiKeywords.Words);
        Assert.True(AnsiKeywords.Words.Count >= 140);
    }

    [Theory]
    [InlineData("SELECT a, b FROM t WHERE x >= 1.5 AND y <> 'z' -- c")]
    [InlineData("select \"Q\" /* c */ from s.t;")]
    [InlineData("a||b::int")]
    [InlineData("it's 'unterminated")]
    [InlineData("1e+ .e 5. 5.e3")]
    [InlineData("\r\n\r\n")]
    [InlineData("ünïcode ident 日本語 'é'")]
    [InlineData("{}[]@#$~^&?`")]
    [InlineData("/* /* nested */ */")]
    [InlineData("--")]
    [InlineData("/*/")]
    [InlineData("'''")]
    [InlineData("\"\"\"\"")]
    public void Tokenize_HandWrittenInputs_AreContiguous(string sql) => AssertContiguous(sql);

    [Fact]
    public void Tokenize_FiftyHandWrittenInputs_AreContiguous()
    {
        var parts = new[] { "SELECT", " ", "'", "''", "\"", "--", "/*", "*/", "1", ".", "e", "<", ">", "=", "!", "|", "(", ")", ",", ";", "x", "\n", "é", "#" };
        var rng = new Random(7);
        for (var i = 0; i < 50; i++)
        {
            AssertContiguous(string.Concat(Enumerable.Range(0, rng.Next(1, 12)).Select(_ => parts[rng.Next(parts.Length)])));
        }
    }

    [Fact]
    public void Tokenize_RandomCharacterFuzz_NeverThrowsAndStaysContiguous()
    {
        var rng = new Random(42);
        for (var i = 0; i < 1000; i++)
        {
            var chars = new char[rng.Next(0, 201)];
            for (var j = 0; j < chars.Length; j++)
            {
                chars[j] = (char)rng.Next(0, 0x250);
            }

            AssertContiguous(new string(chars));
        }
    }

    private static void AssertContiguous(string sql)
    {
        var pos = 0;
        foreach (var token in Tokenizer.Tokenize(sql))
        {
            Assert.Equal(pos, token.Start);
            Assert.True(token.Length > 0);
            Assert.NotEqual(TokenKind.EndOfFile, token.Kind);
            pos = token.End;
        }

        Assert.Equal(sql.Length, pos);
    }
}
