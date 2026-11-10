using Squill.Sql.Analysis;
using Squill.Sql.Lexing;
using Squill.Sql.Syntax;

namespace Squill.Sql.Completion;

/// <summary>What the caret of a table position says about the join it belongs to.</summary>
/// <param name="IsJoin">Whether the position is the table after <c>JOIN</c>, <c>APPLY</c> or a comma of a FROM list.</param>
/// <param name="Kind">The kind of join; <see cref="JoinKind.Comma"/> for a comma.</param>
/// <param name="IsNatural">Whether the join is <c>NATURAL</c>.</param>
/// <param name="IsApply">Whether the join is a T-SQL <c>CROSS APPLY</c> / <c>OUTER APPLY</c>.</param>
/// <param name="HasCondition">Whether an <c>ON</c> or <c>USING</c> already follows the word being typed.</param>
/// <param name="LeftSources">The sources of the same FROM list that precede the position (derived tables and CTEs included).</param>
internal sealed record JoinContext(
    bool IsJoin,
    JoinKind Kind,
    bool IsNatural,
    bool IsApply,
    bool HasCondition,
    IReadOnlyList<VisibleSource> LeftSources)
{
    /// <summary>The context of every position that is not a join.</summary>
    public static JoinContext None { get; } = new(false, JoinKind.Inner, false, false, false, []);
}

/// <summary>Reads the join context at a table position from the tokens around the word being typed.</summary>
internal static class JoinContextAnalyzer
{
    /// <summary>Analyzes the table position whose typed word spans <paramref name="wordStart"/> to <paramref name="wordEnd"/>.</summary>
    /// <param name="request">The completion request.</param>
    /// <param name="wordStart">The start of the identifier being typed (the caret when none).</param>
    /// <param name="wordEnd">The end of the identifier being typed.</param>
    /// <returns>The context, <see cref="JoinContext.None"/> when the position is not a join.</returns>
    public static JoinContext Analyze(SqlRequest request, int wordStart, int wordEnd)
    {
        var text = request.Text;
        wordStart = Math.Clamp(wordStart, 0, text.Length);
        wordEnd = Math.Clamp(wordEnd, wordStart, text.Length);
        var tokenizer = SqlTokenizers.For(request.Dialect);
        var before = text[..wordStart];
        var words = Significant(tokenizer, before);
        var i = words.Count - 1;
        if (i >= 0 && Is(words[i], "LATERAL"))
        {
            i--;
        }

        if (i < 0)
        {
            return JoinContext.None;
        }

        // The typed word is removed: a half-typed "ord" would otherwise parse as a table called "ord".
        var cleaned = before + text[wordEnd..];
        var scope = SqlScopeResolver.Resolve(SqlParser.Parse(cleaned, request.Dialect), wordStart);
        var left = scope.Sources.Take(scope.LocalCount).ToList();

        if (words[i] == ",")
        {
            return scope.Clause == ClauseKind.From ? new JoinContext(true, JoinKind.Comma, false, false, false, left) : JoinContext.None;
        }

        var apply = Is(words[i], "APPLY");
        if (!apply && !Is(words[i], "JOIN"))
        {
            return JoinContext.None;
        }

        i--;
        var kind = JoinKind.Inner;
        if (apply)
        {
            kind = i >= 0 && Is(words[i], "OUTER") ? JoinKind.Left : JoinKind.Cross;
            i--;
        }
        else
        {
            if (i >= 0 && Is(words[i], "OUTER"))
            {
                i--;
            }

            if (i >= 0)
            {
                switch (words[i].ToUpperInvariant())
                {
                    case "LEFT":
                        kind = JoinKind.Left;
                        i--;
                        break;
                    case "RIGHT":
                        kind = JoinKind.Right;
                        i--;
                        break;
                    case "FULL":
                        kind = JoinKind.Full;
                        i--;
                        break;
                    case "CROSS":
                        kind = JoinKind.Cross;
                        i--;
                        break;
                    case "INNER":
                        i--;
                        break;
                }
            }
        }

        var natural = i >= 0 && Is(words[i], "NATURAL");
        var after = Significant(tokenizer, text[wordEnd..]);
        var hasCondition = after.Count > 0 && (Is(after[0], "ON") || Is(after[0], "USING"));
        return new JoinContext(true, kind, natural, apply, hasCondition, left);
    }

    private static bool Is(string word, string keyword) => word.Equals(keyword, StringComparison.OrdinalIgnoreCase);

    private static List<string> Significant(ISqlTokenizer tokenizer, string text) =>
        [.. tokenizer.Tokenize(text)
            .Where(t => t.Kind is not (TokenKind.Whitespace or TokenKind.LineComment or TokenKind.BlockComment or TokenKind.EndOfFile))
            .Select(t => t.Text(text))];
}
