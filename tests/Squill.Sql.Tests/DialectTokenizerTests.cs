using Squill.Core;
using Squill.Sql.Lexing;
using Xunit;

namespace Squill.Sql.Tests;

public sealed class DialectTokenizerTests
{
    private static (TokenKind Kind, string Text)[] Lex(DatabaseDialect dialect, string sql) =>
        [.. SqlTokenizers.For(dialect).Tokenize(sql).Select(t => (t.Kind, t.Text(sql)))];

    [Fact]
    public void For_PostgresAndSqlite_ReturnDedicatedTokenizers()
    {
        Assert.IsType<PostgresTokenizer>(SqlTokenizers.For(DatabaseDialect.PostgreSql));
        Assert.IsType<SqliteTokenizer>(SqlTokenizers.For(DatabaseDialect.Sqlite));
    }

    [Theory]
    [InlineData("$$a;b$$", TokenKind.StringLiteral)]
    [InlineData("$fn$ x $$ y $fn$", TokenKind.StringLiteral)]
    [InlineData("$$unterminated", TokenKind.StringLiteral)]
    [InlineData("$t$ open $x$", TokenKind.StringLiteral)]
    [InlineData("E'a\\'b'", TokenKind.StringLiteral)]
    [InlineData("e'it''s'", TokenKind.StringLiteral)]
    [InlineData("E'abc\\", TokenKind.StringLiteral)]
    [InlineData("/* a /* b */ c */", TokenKind.BlockComment)]
    [InlineData("/* a /* b */", TokenKind.BlockComment)]
    [InlineData("::", TokenKind.Operator)]
    [InlineData("$1", TokenKind.Parameter)]
    [InlineData("$12", TokenKind.Parameter)]
    [InlineData("U&\"d\\0061t\"", TokenKind.QuotedIdentifier)]
    [InlineData("ilike", TokenKind.Keyword)]
    [InlineData("RETURNING", TokenKind.Keyword)]
    [InlineData("lateral", TokenKind.Keyword)]
    public void Postgres_SingleToken_CoversAllText(string sql, TokenKind kind) =>
        Assert.Equal([(kind, sql)], Lex(DatabaseDialect.PostgreSql, sql));

    [Fact]
    public void Postgres_DollarWithoutDelimiter_IsUnknown() =>
        Assert.Equal([(TokenKind.Unknown, "$")], Lex(DatabaseDialect.PostgreSql, "$"));

    [Fact]
    public void Postgres_Cast_SplitsOperator() =>
        Assert.Equal(
            [(TokenKind.Identifier, "a"), (TokenKind.Operator, "::"), (TokenKind.Keyword, "int")],
            Lex(DatabaseDialect.PostgreSql, "a::int"));

    [Fact]
    public void Postgres_IdentifierEndingInE_IsNotEscapeString() =>
        Assert.Equal(
            [(TokenKind.Identifier, "name"), (TokenKind.StringLiteral, "'x'")], Lex(DatabaseDialect.PostgreSql, "name'x'"));

    [Theory]
    [InlineData("[my col]", TokenKind.QuotedIdentifier)]
    [InlineData("[open", TokenKind.QuotedIdentifier)]
    [InlineData("`a``b`", TokenKind.QuotedIdentifier)]
    [InlineData("`open", TokenKind.QuotedIdentifier)]
    [InlineData("?", TokenKind.Parameter)]
    [InlineData("?12", TokenKind.Parameter)]
    [InlineData(":name", TokenKind.Parameter)]
    [InlineData("@name", TokenKind.Parameter)]
    [InlineData("$name", TokenKind.Parameter)]
    [InlineData("-- c", TokenKind.LineComment)]
    [InlineData("/* c */", TokenKind.BlockComment)]
    [InlineData("pragma", TokenKind.Keyword)]
    [InlineData("AUTOINCREMENT", TokenKind.Keyword)]
    [InlineData("without", TokenKind.Keyword)]
    [InlineData("rowid", TokenKind.Keyword)]
    public void Sqlite_SingleToken_CoversAllText(string sql, TokenKind kind) =>
        Assert.Equal([(kind, sql)], Lex(DatabaseDialect.Sqlite, sql));

    [Theory]
    [InlineData(":")]
    [InlineData("@")]
    [InlineData("$")]
    public void Sqlite_BareParameterSigil_IsUnknown(string sql) =>
        Assert.Equal([(TokenKind.Unknown, sql)], Lex(DatabaseDialect.Sqlite, sql));

    [Fact]
    public void Sqlite_ParametersInStatement_AreSeparateTokens() =>
        Assert.Equal(
            [(TokenKind.Parameter, "?1"), (TokenKind.Punctuation, ","), (TokenKind.Parameter, ":a")],
            Lex(DatabaseDialect.Sqlite, "?1,:a"));

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql)]
    [InlineData(DatabaseDialect.Sqlite)]
    public void Tokenize_HandWrittenAndFuzz_StaysContiguous(DatabaseDialect dialect)
    {
        var tokenizer = SqlTokenizers.For(dialect);
        var parts = new[]
        {
            "SELECT", " ", "'", "''", "\"", "--", "/*", "*/", "1", ".", "e", "E'", "\\", "$$", "$a$", "$1", "$", "::",
            ":", "?", "@x", "[", "]", "`", "U&", "(", ")", ",", ";", "x", "\n", "é", "#",
        };
        var rng = new Random(7);
        for (var i = 0; i < 200; i++)
        {
            AssertContiguous(tokenizer, string.Concat(Enumerable.Range(0, rng.Next(1, 14)).Select(_ => parts[rng.Next(parts.Length)])));
        }

        rng = new Random(42);
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
