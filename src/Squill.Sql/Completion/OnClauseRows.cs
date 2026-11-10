using Squill.Core;
using Squill.Sql.Analysis;
using Squill.Sql.Lexing;

namespace Squill.Sql.Completion;

/// <summary>The join a caret inside an <c>ON</c> clause belongs to.</summary>
/// <param name="New">The source after the <c>JOIN</c> that owns the clause.</param>
/// <param name="LeftSources">Every source of the FROM list before <paramref name="New"/>.</param>
/// <param name="Written">The <c>a.x = b.y</c> conjuncts already written before the caret, each as two lower-case <c>qualifier.name</c> texts.</param>
internal sealed record OnContext(VisibleSource New, IReadOnlyList<VisibleSource> LeftSources, IReadOnlyList<(string A, string B)> Written);

/// <summary>Proposes the conditions of a join inside its <c>ON</c> clause, with the same rules as the rows after <c>JOIN</c>.</summary>
internal static class OnClauseRows
{
    /// <summary>Finds the join that owns the <c>ON</c> clause at the caret.</summary>
    /// <param name="request">The completion request.</param>
    /// <param name="wordStart">The start of the identifier being typed.</param>
    /// <param name="local">The sources of the innermost statement, in FROM order.</param>
    /// <returns>The context, or <see langword="null"/> for <c>CROSS</c>, <c>NATURAL</c>, <c>USING</c>, comma and unknown positions.</returns>
    public static OnContext? AnalyzeOn(SqlRequest request, int wordStart, IReadOnlyList<VisibleSource> local)
    {
        var before = request.Text[..Math.Clamp(wordStart, 0, request.Text.Length)];
        var words = SqlTokenizers.For(request.Dialect).Tokenize(before)
            .Where(t => t.Kind is not (TokenKind.Whitespace or TokenKind.LineComment or TokenKind.BlockComment or TokenKind.EndOfFile))
            .Select(t => (t.Kind, Text: t.Text(before)))
            .ToList();

        var depth = 0;
        var separators = 0;
        var onIndex = -1;
        var closestChecked = false;
        var fromFound = false;
        for (var i = words.Count - 1; i >= 0 && !fromFound; i--)
        {
            var word = words[i].Text;
            if (word == ")")
            {
                depth++;
            }
            else if (word == "(")
            {
                if (depth == 0)
                {
                    return null;
                }

                depth--;
            }
            else if (depth == 0)
            {
                switch (word.ToUpperInvariant())
                {
                    case "FROM":
                        fromFound = true;
                        break;
                    case "USING":
                        return null;
                    case "ON" when !closestChecked:
                        onIndex = i;
                        break;
                    case "JOIN" or "APPLY" or ",":
                        if (!closestChecked)
                        {
                            closestChecked = true;
                            var previous = i > 0 ? words[i - 1].Text : string.Empty;
                            if (!word.Equals("JOIN", StringComparison.OrdinalIgnoreCase) || previous.Equals("CROSS", StringComparison.OrdinalIgnoreCase) || previous.Equals("NATURAL", StringComparison.OrdinalIgnoreCase))
                            {
                                return null;
                            }
                        }

                        separators++;
                        break;
                }
            }
        }

        if (!fromFound || !closestChecked || onIndex < 0 || separators >= local.Count)
        {
            return null;
        }

        return new OnContext(local[separators], [.. local.Take(separators)], Conjuncts(words, onIndex + 1));
    }

    /// <summary>Builds the condition rows of the join at the caret.</summary>
    /// <param name="request">The completion request.</param>
    /// <param name="schema">The schema snapshot.</param>
    /// <param name="scope">The scope at the caret.</param>
    /// <param name="wordStart">The start of the identifier being typed.</param>
    /// <param name="range">The prefix being typed.</param>
    /// <returns>The rows, best first.</returns>
    public static IReadOnlyList<CompletionItem> Build(SqlRequest request, SchemaSnapshot schema, ScopeInfo scope, int wordStart, TextRange range)
    {
        var local = scope.Sources.Take(scope.LocalCount).ToList();
        if (AnalyzeOn(request, wordStart, local) is not { } on
            || on.New.DerivedColumns is not null
            || schema.FindTable(on.New.Schema, on.New.Name) is not { } candidate)
        {
            return [];
        }

        var items = new List<CompletionItem>();
        var newName = on.New.Alias ?? on.New.Name;
        foreach (var relation in TableRelationFinder.Find(schema, on.LeftSources, candidate))
        {
            var existingName = relation.Existing.Alias ?? relation.Existing.Name;
            if (AlreadyWritten(relation, existingName, newName, on.Written))
            {
                continue;
            }

            var condition = JoinConditionText.Build(relation, existingName, newName, request.Dialect, KeywordCasing.UseLowerKeywords(request, wordStart, request.Offset - wordStart));
            items.Add(new CompletionItem(
                condition,
                CompletionKind.Snippet,
                condition,
                relation.ForeignKeyName ?? "same column name",
                CompletionOrder.Relation + items.Count.ToString("D6", System.Globalization.CultureInfo.InvariantCulture),
                range,
                Documentation: CompletionDocumentation.Relation(schema, relation)));
        }

        return items;
    }

    private static bool AlreadyWritten(TableRelation relation, string existingName, string newName, IReadOnlyList<(string A, string B)> written)
    {
        for (var i = 0; i < relation.ExistingColumns.Count; i++)
        {
            var a = (existingName + "." + relation.ExistingColumns[i]).ToLowerInvariant();
            var b = (newName + "." + relation.OtherColumns[i]).ToLowerInvariant();
            if (!written.Any(w => (w.A == a && w.B == b) || (w.A == b && w.B == a)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reads the top-level <c>AND</c> conjuncts after the <c>ON</c> that have the shape <c>a.x = b.y</c>.</summary>
    private static List<(string, string)> Conjuncts(List<(TokenKind Kind, string Text)> words, int start)
    {
        var result = new List<(string, string)>();
        var depth = 0;
        var segment = new List<(TokenKind Kind, string Text)>();
        for (var i = start; i <= words.Count; i++)
        {
            var end = i == words.Count;
            if (!end)
            {
                var text = words[i].Text;
                depth += text == "(" ? 1 : text == ")" ? -1 : 0;
                if (!(depth == 0 && text.Equals("AND", StringComparison.OrdinalIgnoreCase)))
                {
                    segment.Add(words[i]);
                    continue;
                }
            }

            if (segment.Count == 7 && segment[1].Text == "." && segment[3].Text == "=" && segment[5].Text == "."
                && segment[0].Kind != TokenKind.Punctuation && segment[2].Kind != TokenKind.Punctuation
                && segment[4].Kind != TokenKind.Punctuation && segment[6].Kind != TokenKind.Punctuation)
            {
                result.Add((Unquote(segment[0]) + "." + Unquote(segment[2]), Unquote(segment[4]) + "." + Unquote(segment[6])));
            }

            segment.Clear();
        }

        return result;
    }

    private static string Unquote((TokenKind Kind, string Text) token) =>
        (token.Kind == TokenKind.QuotedIdentifier && token.Text.Length >= 2 ? token.Text[1..^1] : token.Text).ToLowerInvariant();
}
