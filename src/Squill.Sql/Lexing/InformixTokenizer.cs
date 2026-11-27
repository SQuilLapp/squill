using System.Collections.Frozen;
using Squill.Sql.Keywords;

namespace Squill.Sql.Lexing;

/// <summary>Tokenizer for Informix SQL: <c>{ ... }</c> comments, <c>?</c> and <c>:name</c> parameters.</summary>
public sealed class InformixTokenizer : SqlTokenizerBase
{
    /// <inheritdoc />
    protected override FrozenSet<string> Keywords => AnsiKeywords.Words;

    /// <inheritdoc />
    protected override bool TryReadSpecial(string text, int pos, out Token token)
    {
        var c = text[pos];
        var next = pos + 1 < text.Length ? text[pos + 1] : '\0';
        token = default;

        if (c == '{')
        {
            var end = text.IndexOf('}', pos + 1);
            token = new Token(TokenKind.BlockComment, pos, (end < 0 ? text.Length : end + 1) - pos);
        }
        else if (c == ':' && IsIdentifierStart(next))
        {
            var end = pos + 2;
            while (end < text.Length && IsIdentifierPart(text[end]))
            {
                end++;
            }

            token = new Token(TokenKind.Parameter, pos, end - pos);
        }

        return token.Length > 0;
    }
}
