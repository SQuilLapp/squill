using System.Collections.Frozen;
using Squill.Sql.Keywords;

namespace Squill.Sql.Lexing;

/// <summary>Tokenizer for SQLite: <c>[x]</c> and backtick identifiers, <c>?</c>, <c>?N</c>, <c>:x</c>, <c>@x</c>, <c>$x</c> parameters.</summary>
public sealed class SqliteTokenizer : SqlTokenizerBase
{
    /// <inheritdoc />
    protected override FrozenSet<string> Keywords => SqliteKeywords.Words;

    /// <inheritdoc />
    protected override bool TryReadSpecial(string text, int pos, out Token token)
    {
        var c = text[pos];
        token = default;

        if (c == '[')
        {
            var close = text.IndexOf(']', pos + 1);
            token = new Token(TokenKind.QuotedIdentifier, pos, (close < 0 ? text.Length : close + 1) - pos);
        }
        else if (c == '`')
        {
            token = Quoted(TokenKind.QuotedIdentifier, text, pos, '`');
        }
        else if (c == '?')
        {
            token = new Token(TokenKind.Parameter, pos, 1 + CountWhile(text, pos + 1, char.IsAsciiDigit));
        }
        else if (c is ':' or '@' or '$')
        {
            var length = CountWhile(text, pos + 1, IsIdentifierPart);
            if (length > 0)
            {
                token = new Token(TokenKind.Parameter, pos, 1 + length);
            }
        }

        return token.Length > 0;
    }

    private static int CountWhile(string text, int start, Func<char, bool> accept)
    {
        var i = start;
        while (i < text.Length && accept(text[i]))
        {
            i++;
        }

        return i - start;
    }
}
