using Squill.Sql.Analysis;
using Squill.Sql.Lexing;
using Squill.Sql.Syntax;

namespace Squill.Sql.Completion;

/// <summary>Decides when typing a space should open the completion popup by itself.</summary>
public static class CompletionTrigger
{
    /// <summary>Tells whether the space just typed before the caret follows a keyword (or a FROM-list comma) that expects a table, column or condition.</summary>
    /// <param name="request">The request; only the text before the caret is read and no schema is needed.</param>
    /// <returns><see langword="true"/> after <c>FROM</c>, <c>JOIN</c>, <c>INTO</c>, <c>UPDATE</c>, <c>ON</c>, <c>USING</c>, <c>SELECT</c> and a comma of a FROM list; <see langword="false"/> elsewhere, inside strings and comments, and for an empty text.</returns>
    public static bool OpensAfterSpace(SqlRequest request)
    {
        var offset = Math.Clamp(request.Offset, 0, request.Text.Length);
        if (offset == 0 || request.Text[offset - 1] != ' ')
        {
            return false;
        }

        var before = request.Text[..offset];
        string? last = null;
        var lastIsSpace = false;
        foreach (var token in SqlTokenizers.For(request.Dialect).Tokenize(before))
        {
            if (token.Kind == TokenKind.EndOfFile)
            {
                continue;
            }

            lastIsSpace = token.Kind == TokenKind.Whitespace;
            if (!lastIsSpace)
            {
                last = token.Kind is TokenKind.Keyword or TokenKind.Identifier or TokenKind.Punctuation ? token.Text(before) : string.Empty;
            }
        }

        // A string or a line comment that is still open swallows the trailing space, so the last token is not whitespace then.
        if (!lastIsSpace || last is null)
        {
            return false;
        }

        if (last == ",")
        {
            return SqlScopeResolver.Resolve(SqlParser.Parse(before, request.Dialect), offset).Clause == ClauseKind.From;
        }

        return last.ToUpperInvariant() is "FROM" or "JOIN" or "INTO" or "UPDATE" or "ON" or "USING" or "SELECT";
    }
}
