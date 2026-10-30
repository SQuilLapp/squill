using System.Collections.Frozen;
using Squill.Sql.Keywords;

namespace Squill.Sql.Lexing;

/// <summary>Tokenizer for PostgreSQL: dollar quoting, <c>E''</c> strings, nested comments, <c>::</c>, <c>$n</c> parameters.</summary>
public sealed class PostgresTokenizer : SqlTokenizerBase
{
    /// <inheritdoc />
    protected override FrozenSet<string> Keywords => PostgresKeywords.Words;

    /// <inheritdoc />
    protected override bool IsIdentifierPart(char c) => base.IsIdentifierPart(c) || c == '$';

    /// <inheritdoc />
    protected override bool TryReadSpecial(string text, int pos, out Token token)
    {
        var c = text[pos];
        var next = pos + 1 < text.Length ? text[pos + 1] : '\0';
        token = default;

        if (c == ':' && next == ':')
        {
            token = new Token(TokenKind.Operator, pos, 2);
        }
        else if (c == '/' && next == '*')
        {
            token = new Token(TokenKind.BlockComment, pos, ReadNestedComment(text, pos) - pos);
        }
        else if (c is 'E' or 'e' && next == '\'')
        {
            token = new Token(TokenKind.StringLiteral, pos, ReadEscapeString(text, pos + 1) - pos);
        }
        else if (c is 'U' or 'u' && next == '&' && pos + 2 < text.Length && text[pos + 2] == '"')
        {
            var quoted = Quoted(TokenKind.QuotedIdentifier, text, pos + 2, '"');
            token = new Token(TokenKind.QuotedIdentifier, pos, quoted.End - pos);
        }
        else if (c == '$')
        {
            token = ReadDollar(text, pos, next);
        }

        return token.Length > 0;
    }

    private static int ReadEscapeString(string text, int quotePos)
    {
        var i = quotePos + 1;
        while (i < text.Length)
        {
            if (text[i] == '\\')
            {
                i += 2;
            }
            else if (text[i] == '\'')
            {
                if (i + 1 < text.Length && text[i + 1] == '\'')
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

        return text.Length;
    }

    private Token ReadDollar(string text, int pos, char next)
    {
        if (char.IsAsciiDigit(next))
        {
            var end = pos + 1;
            while (end < text.Length && char.IsAsciiDigit(text[end]))
            {
                end++;
            }

            return new Token(TokenKind.Parameter, pos, end - pos);
        }

        var tagEnd = pos + 1;
        if (IsIdentifierStart(next))
        {
            while (tagEnd < text.Length && text[tagEnd] != '$' && IsIdentifierPart(text[tagEnd]))
            {
                tagEnd++;
            }
        }

        if (tagEnd >= text.Length || text[tagEnd] != '$')
        {
            return default;
        }

        var delimiter = text.AsSpan(pos, tagEnd + 1 - pos);
        var close = text.AsSpan(tagEnd + 1).IndexOf(delimiter, StringComparison.Ordinal);
        var stop = close < 0 ? text.Length : tagEnd + 1 + close + delimiter.Length;
        return new Token(TokenKind.StringLiteral, pos, stop - pos);
    }
}
