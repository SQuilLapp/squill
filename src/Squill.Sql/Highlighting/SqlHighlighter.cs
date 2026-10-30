using Squill.Core;
using Squill.Sql.Functions;
using Squill.Sql.Lexing;

namespace Squill.Sql.Highlighting;

/// <summary>Classifies SQL text into highlight spans, for editors without a TextMate engine.</summary>
public static class SqlHighlighter
{
    /// <summary>Classifies <paramref name="text"/>.</summary>
    /// <param name="text">The SQL text.</param>
    /// <param name="dialect">The database engine.</param>
    /// <returns>Spans in text order; whitespace, punctuation and unknown characters are omitted.</returns>
    public static IReadOnlyList<HighlightSpan> Classify(string text, DatabaseDialect dialect)
    {
        var tokens = SqlTokenizers.For(dialect).Tokenize(text);
        var functions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var function in FunctionCatalog.For(dialect))
        {
            functions.Add(function.Name);
        }

        var spans = new List<HighlightSpan>();
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            HighlightKind? kind = token.Kind switch
            {
                TokenKind.Keyword => HighlightKind.Keyword,
                TokenKind.Identifier => IsFunctionCall(text, tokens, i, functions) ? HighlightKind.Function : HighlightKind.Identifier,
                TokenKind.QuotedIdentifier => HighlightKind.QuotedIdentifier,
                TokenKind.StringLiteral => HighlightKind.StringLiteral,
                TokenKind.NumberLiteral => HighlightKind.Number,
                TokenKind.LineComment or TokenKind.BlockComment => HighlightKind.Comment,
                TokenKind.Operator => HighlightKind.Operator,
                TokenKind.Parameter => HighlightKind.Parameter,
                _ => null,
            };

            if (kind is { } k)
            {
                spans.Add(new HighlightSpan(token.Start, token.Length, k));
            }
        }

        return spans;
    }

    private static bool IsFunctionCall(string text, IReadOnlyList<Token> tokens, int index, HashSet<string> functions) =>
        index + 1 < tokens.Count
        && tokens[index + 1] is { Kind: TokenKind.Punctuation } next
        && text[next.Start] == '('
        && functions.Contains(tokens[index].Text(text));
}
