using System.Collections.Frozen;

namespace Squill.Sql.Lexing;

/// <summary>Shared tokenizer loop; dialects customize it through the virtual hooks.</summary>
public abstract class SqlTokenizerBase : ISqlTokenizer
{
    /// <summary>Gets the reserved words of the dialect (case-insensitive).</summary>
    protected abstract FrozenSet<string> Keywords { get; }

    /// <inheritdoc />
    public IReadOnlyList<Token> Tokenize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var tokens = new List<Token>();
        var pos = 0;
        while (pos < text.Length)
        {
            if (!TryReadSpecial(text, pos, out var token))
            {
                token = ReadStandard(text, pos);
            }

            tokens.Add(token);
            pos = token.End;
        }

        return tokens;
    }

    /// <summary>Gives a dialect the chance to read a token before the standard rules.</summary>
    /// <param name="text">The SQL text.</param>
    /// <param name="pos">Offset of the next unread character.</param>
    /// <param name="token">The token read, starting at <paramref name="pos"/> with a positive length.</param>
    /// <returns><see langword="true"/> when a token was read.</returns>
    protected virtual bool TryReadSpecial(string text, int pos, out Token token)
    {
        token = default;
        return false;
    }

    /// <summary>Tells whether a character can start an unquoted identifier.</summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for letters and underscore.</returns>
    protected virtual bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    /// <summary>Tells whether a character can continue an unquoted identifier.</summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for letters, digits and underscore.</returns>
    protected virtual bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';

    private Token ReadStandard(string text, int pos)
    {
        var c = text[pos];
        var next = pos + 1 < text.Length ? text[pos + 1] : '\0';

        if (char.IsWhiteSpace(c))
        {
            return Run(TokenKind.Whitespace, text, pos, char.IsWhiteSpace);
        }

        if (c == '-' && next == '-')
        {
            var end = text.AsSpan(pos).IndexOfAny('\r', '\n');
            return new Token(TokenKind.LineComment, pos, end < 0 ? text.Length - pos : end);
        }

        if (c == '/' && next == '*')
        {
            var end = text.IndexOf("*/", pos + 2, StringComparison.Ordinal);
            return new Token(TokenKind.BlockComment, pos, (end < 0 ? text.Length : end + 2) - pos);
        }

        if (c == '\'')
        {
            return Quoted(TokenKind.StringLiteral, text, pos, '\'');
        }

        if (c == '"')
        {
            return Quoted(TokenKind.QuotedIdentifier, text, pos, '"');
        }

        if (char.IsAsciiDigit(c) || (c == '.' && char.IsAsciiDigit(next)))
        {
            return new Token(TokenKind.NumberLiteral, pos, ReadNumber(text, pos));
        }

        if (IsIdentifierStart(c))
        {
            var token = Run(TokenKind.Identifier, text, pos, IsIdentifierPart);
            return Keywords.Contains(text.Substring(token.Start, token.Length))
                ? token with { Kind = TokenKind.Keyword }
                : token;
        }

        if (c is '(' or ')' or ',' or ';' or '.')
        {
            return new Token(TokenKind.Punctuation, pos, 1);
        }

        if ((c == '<' && next is '=' or '>') || (c is '>' or '!' && next == '=') || (c == '|' && next == '|'))
        {
            return new Token(TokenKind.Operator, pos, 2);
        }

        return new Token(c is '+' or '-' or '*' or '/' or '%' or '=' or '<' or '>' ? TokenKind.Operator : TokenKind.Unknown, pos, 1);
    }

    private static Token Run(TokenKind kind, string text, int pos, Func<char, bool> accept)
    {
        var end = pos + 1;
        while (end < text.Length && accept(text[end]))
        {
            end++;
        }

        return new Token(kind, pos, end - pos);
    }

    /// <summary>Reads a quoted run where a doubled quote character is an escape; unterminated runs to the end.</summary>
    /// <param name="kind">Kind of the token produced.</param>
    /// <param name="text">The SQL text.</param>
    /// <param name="pos">Offset of the opening quote.</param>
    /// <param name="quote">The quote character.</param>
    /// <returns>The token.</returns>
    protected static Token Quoted(TokenKind kind, string text, int pos, char quote)
    {
        var i = pos + 1;
        while (i < text.Length)
        {
            if (text[i] == quote)
            {
                if (i + 1 < text.Length && text[i + 1] == quote)
                {
                    i += 2;
                    continue;
                }

                return new Token(kind, pos, i + 1 - pos);
            }

            i++;
        }

        return new Token(kind, pos, text.Length - pos);
    }

    /// <summary>Reads a block comment that nests; unterminated runs to the end.</summary>
    /// <param name="text">The SQL text.</param>
    /// <param name="pos">Offset of the opening <c>/*</c>.</param>
    /// <returns>Offset just past the comment.</returns>
    protected static int ReadNestedComment(string text, int pos)
    {
        var depth = 0;
        var i = pos;
        while (i < text.Length)
        {
            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                depth++;
                i += 2;
            }
            else if (text[i] == '*' && i + 1 < text.Length && text[i + 1] == '/')
            {
                depth--;
                i += 2;
                if (depth == 0)
                {
                    return i;
                }
            }
            else
            {
                i++;
            }
        }

        return text.Length;
    }

    private static int ReadNumber(string text, int pos)
    {
        var i = SkipDigits(text, pos);
        if (i < text.Length && text[i] == '.')
        {
            i = SkipDigits(text, i + 1);
        }

        if (i < text.Length && text[i] is 'e' or 'E')
        {
            var j = i + 1;
            if (j < text.Length && text[j] is '+' or '-')
            {
                j++;
            }

            if (j < text.Length && char.IsAsciiDigit(text[j]))
            {
                i = SkipDigits(text, j);
            }
        }

        return i - pos;
    }

    private static int SkipDigits(string text, int i)
    {
        while (i < text.Length && char.IsAsciiDigit(text[i]))
        {
            i++;
        }

        return i;
    }
}
