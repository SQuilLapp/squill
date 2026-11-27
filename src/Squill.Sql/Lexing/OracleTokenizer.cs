using System.Collections.Frozen;
using Squill.Sql.Keywords;

namespace Squill.Sql.Lexing;

/// <summary>Tokenizer for Oracle SQL and PL/SQL: <c>q'[...]'</c> alternative quoting, <c>N''</c> strings, <c>:name</c> and <c>:1</c> bind parameters, <c>$</c> and <c>#</c> in names.</summary>
public sealed class OracleTokenizer : SqlTokenizerBase
{
    /// <inheritdoc />
    protected override FrozenSet<string> Keywords => OracleKeywords.Words;

    /// <inheritdoc />
    protected override bool IsIdentifierPart(char c) => base.IsIdentifierPart(c) || c is '$' or '#';

    /// <inheritdoc />
    protected override bool TryReadSpecial(string text, int pos, out Token token)
    {
        var c = text[pos];
        var next = pos + 1 < text.Length ? text[pos + 1] : '\0';
        token = default;

        // q'[..]', Q'!..!', nq'{..}': the first character after the quote picks the closing delimiter
        var q = c is 'N' or 'n' && next is 'Q' or 'q' ? pos + 1 : pos;
        if (text[q] is 'Q' or 'q' && q + 2 < text.Length && text[q + 1] == '\'' && !char.IsWhiteSpace(text[q + 2]))
        {
            var open = text[q + 2];
            var close = open switch { '[' => ']', '(' => ')', '{' => '}', '<' => '>', _ => open };
            var end = text.IndexOf(close + "'", q + 3, StringComparison.Ordinal);
            token = new Token(TokenKind.StringLiteral, pos, (end < 0 ? text.Length : end + 2) - pos);
        }
        else if (c is 'N' or 'n' && next == '\'')
        {
            var quoted = Quoted(TokenKind.StringLiteral, text, pos + 1, '\'');
            token = new Token(TokenKind.StringLiteral, pos, quoted.End - pos);
        }
        else if (c == ':' && (IsIdentifierStart(next) || char.IsAsciiDigit(next)))
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
