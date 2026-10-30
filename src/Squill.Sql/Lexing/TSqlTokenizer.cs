using System.Collections.Frozen;
using Squill.Sql.Keywords;

namespace Squill.Sql.Lexing;

/// <summary>Tokenizer for SQL Server T-SQL: <c>[x]</c> identifiers, <c>N''</c> strings, <c>@x</c> parameters, <c>#temp</c> names, nested comments.</summary>
public sealed class TSqlTokenizer : SqlTokenizerBase
{
    /// <inheritdoc />
    protected override FrozenSet<string> Keywords => TSqlKeywords.Words;

    /// <inheritdoc />
    protected override bool IsIdentifierPart(char c) => base.IsIdentifierPart(c) || c is '$' or '#';

    /// <inheritdoc />
    protected override bool TryReadSpecial(string text, int pos, out Token token)
    {
        var c = text[pos];
        var next = pos + 1 < text.Length ? text[pos + 1] : '\0';
        token = default;

        if (c == '[')
        {
            token = new Token(TokenKind.QuotedIdentifier, pos, ReadBracketed(text, pos) - pos);
        }
        else if (c is 'N' or 'n' && next == '\'')
        {
            var quoted = Quoted(TokenKind.StringLiteral, text, pos + 1, '\'');
            token = new Token(TokenKind.StringLiteral, pos, quoted.End - pos);
        }
        else if (c == '/' && next == '*')
        {
            token = new Token(TokenKind.BlockComment, pos, ReadNestedComment(text, pos) - pos);
        }
        else if (c is '@' or '#')
        {
            var doubled = next == c;
            var start = pos + (doubled ? 2 : 1);
            var end = start;
            while (end < text.Length && IsIdentifierPart(text[end]))
            {
                end++;
            }

            if (end > start)
            {
                token = new Token(c == '@' && !doubled ? TokenKind.Parameter : TokenKind.Identifier, pos, end - pos);
            }
        }

        return token.Length > 0;
    }

    private static int ReadBracketed(string text, int pos)
    {
        var i = pos + 1;
        while (i < text.Length)
        {
            if (text[i] == ']')
            {
                if (i + 1 < text.Length && text[i + 1] == ']')
                {
                    i += 2;
                    continue;
                }

                return i + 1;
            }

            i++;
        }

        return text.Length;
    }
}
