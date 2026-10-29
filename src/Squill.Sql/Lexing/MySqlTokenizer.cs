using System.Collections.Frozen;
using Squill.Sql.Keywords;

namespace Squill.Sql.Lexing;

/// <summary>Tokenizer for MySQL and MariaDB: backtick identifiers, <c>#</c> comments, backslash escapes, <c>?</c> and <c>@name</c> parameters.</summary>
public sealed class MySqlTokenizer : SqlTokenizerBase
{
    /// <inheritdoc />
    protected override FrozenSet<string> Keywords => MySqlKeywords.Words;

    /// <inheritdoc />
    protected override bool IsIdentifierPart(char c) => base.IsIdentifierPart(c) || c == '$';

    /// <inheritdoc />
    protected override bool TryReadSpecial(string text, int pos, out Token token)
    {
        var c = text[pos];
        var next = pos + 1 < text.Length ? text[pos + 1] : '\0';
        token = default;

        if (c == '`')
        {
            token = Quoted(TokenKind.QuotedIdentifier, text, pos, '`');
        }
        else if (c == '#')
        {
            token = LineComment(text, pos);
        }
        else if (c == '-' && next == '-')
        {
            // "--" only starts a comment before whitespace or the end of the text.
            token = pos + 2 >= text.Length || char.IsWhiteSpace(text[pos + 2])
                ? LineComment(text, pos)
                : new Token(TokenKind.Operator, pos, 1);
        }
        else if (c is '\'' or '"')
        {
            token = new Token(TokenKind.StringLiteral, pos, ReadBackslashString(text, pos) - pos);
        }
        else if (c == '?')
        {
            token = new Token(TokenKind.Parameter, pos, 1);
        }
        else if (c == '@')
        {
            var system = next == '@';
            var start = pos + (system ? 2 : 1);
            var end = start;
            while (end < text.Length && IsIdentifierPart(text[end]))
            {
                end++;
            }

            if (end > start)
            {
                token = new Token(system ? TokenKind.Identifier : TokenKind.Parameter, pos, end - pos);
            }
        }

        return token.Length > 0;
    }

    private static Token LineComment(string text, int pos)
    {
        var end = text.AsSpan(pos).IndexOfAny('\r', '\n');
        return new Token(TokenKind.LineComment, pos, end < 0 ? text.Length - pos : end);
    }

    private static int ReadBackslashString(string text, int pos)
    {
        var quote = text[pos];
        var i = pos + 1;
        while (i < text.Length)
        {
            if (text[i] == '\\')
            {
                i += 2;
            }
            else if (text[i] == quote)
            {
                if (i + 1 < text.Length && text[i + 1] == quote)
                {
                    i += 2;
                    continue;
                }

                return i + 1;
            }
            else
            {
                i++;
            }
        }

        return Math.Min(i, text.Length);
    }
}
