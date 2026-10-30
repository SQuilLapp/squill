using System.Text;
using Squill.Core;
using Squill.Sql.Lexing;
using Squill.Sql.Statements;
using Squill.Sql.Syntax;

namespace Squill.Sql.Formatting;

/// <summary>Deterministic, idempotent, token-based SQL formatter.</summary>
public static class SqlFormatter
{
    /// <summary>Formats a script statement by statement.</summary>
    /// <param name="text">The SQL text.</param>
    /// <param name="dialect">The dialect of the text.</param>
    /// <param name="options">The formatting options.</param>
    /// <returns>The formatted text; statements the parser reports problems for, and text between statements (comments, batch directives), are kept as written.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="FormatOptions.IndentSize"/> is negative.</exception>
    public static string Format(string text, DatabaseDialect dialect, FormatOptions options)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(options.IndentSize);
        var script = SqlParser.Parse(text, dialect);
        var tokens = SqlTokenizers.For(dialect).Tokenize(text);
        var pieces = new List<string>();
        var position = 0;
        foreach (var span in SqlStatementSplitter.Split(text, dialect))
        {
            AddGap(pieces, text, position, span.Start);
            position = span.End;
            if (script.Diagnostics.Any(d => d.Start >= span.Start && d.Start <= span.End))
            {
                pieces.Add(text.Substring(span.Start, span.Length));
                continue;
            }

            var own = tokens.Where(t => t.Kind != TokenKind.Whitespace && t.Start >= span.Start && t.End <= span.End).ToList();
            pieces.Add(new Writer(text, own, options).Run());
        }

        AddGap(pieces, text, position, text.Length);
        return string.Join("\n\n", pieces);
    }

    private static void AddGap(List<string> pieces, string text, int from, int to)
    {
        var gap = text.AsSpan(from, to - from).Trim();
        if (!gap.IsEmpty)
        {
            pieces.Add(gap.ToString());
        }
    }

    /// <summary>Layout state of one nesting level: the statement itself, a subquery or a plain parenthesis.</summary>
    private sealed class Level(bool clause, int indent, int openIndent)
    {
        /// <summary>Gets whether clause keywords break lines at this level.</summary>
        public bool Clause { get; } = clause;

        /// <summary>Gets whether the level is a parenthesised subquery.</summary>
        public bool Subquery { get; init; }

        /// <summary>Gets the indentation level of the clauses at this level.</summary>
        public int Indent { get; } = indent;

        /// <summary>Gets the indentation level of the line holding the opening parenthesis.</summary>
        public int OpenIndent { get; } = openIndent;

        /// <summary>Gets or sets whether commas currently separate select items.</summary>
        public bool InSelectList { get; set; }

        /// <summary>Gets or sets whether the select item list has not started yet.</summary>
        public bool AfterSelect { get; set; }

        /// <summary>Gets or sets whether a join still waits for its ON condition.</summary>
        public bool JoinPending { get; set; }
    }

    /// <summary>Writes the tokens of one statement with the layout rules.</summary>
    private sealed class Writer(string src, List<Token> tokens, FormatOptions options)
    {
        private static readonly HashSet<string> ClauseWords = new(StringComparer.Ordinal)
        {
            "SELECT", "FROM", "WHERE", "HAVING", "LIMIT", "UNION", "INTERSECT", "EXCEPT", "VALUES", "SET", "RETURNING", "GROUP", "ORDER",
        };

        private static readonly HashSet<string> JoinModifiers = new(StringComparer.Ordinal)
        {
            "NATURAL", "LEFT", "RIGHT", "FULL", "INNER", "OUTER", "CROSS",
        };

        private readonly StringBuilder sb = new();
        private readonly Stack<Level> levels = new([new Level(true, 0, 0)]);
        private int lineIndent;
        private bool atLineStart = true;
        private bool needBreak;
        private bool prevOpenedSubquery;
        private int? commentBreakIndent;

        /// <summary>Formats the statement tokens.</summary>
        /// <returns>The formatted statement.</returns>
        public string Run()
        {
            Token? prev = null;
            var unary = false;
            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                var text = token.Text(src);
                var word = WordOf(token);
                var prevWord = prev is { } p ? WordOf(p) : null;
                var prevText = prev is { } q ? q.Text(src) : null;
                var next = i + 1 < tokens.Count ? tokens[i + 1] : (Token?)null;
                var nextWord = next is { } n ? WordOf(n) : null;
                var isPunct = token.Kind == TokenKind.Punctuation;
                var level = levels.Peek();
                var opensSubquery = isPunct && text == "(" && nextWord is "SELECT" or "WITH";

                if (isPunct && text == ")" && levels.Count > 1)
                {
                    if (levels.Pop().Subquery)
                    {
                        NewLine(level.OpenIndent);
                    }
                }
                else
                {
                    var functionCall = next is { } f && f.Kind == TokenKind.Punctuation && src[f.Start] == '(';
                    BreakBefore(level, token, word, prevWord, nextWord, functionCall, opensSubquery);
                }

                if (needBreak && !atLineStart)
                {
                    NewLine(lineIndent);
                }

                needBreak = false;
                if (!atLineStart)
                {
                    var glued = isPunct && text == "(" && !opensSubquery
                        && prev is { Kind: TokenKind.Keyword or TokenKind.Identifier or TokenKind.QuotedIdentifier } pr && token.Start == pr.End;
                    var noSpace = isPunct && text is ")" or "," or ";" or "."
                        || prevText is "(" or "." || text == "::" || prevText == "::" || unary || glued;
                    if (!noSpace)
                    {
                        sb.Append(' ');
                    }
                }

                sb.Append(token.Kind switch
                {
                    TokenKind.Keyword => options.KeywordCase switch
                    {
                        KeywordCase.Upper => text.ToUpperInvariant(),
                        KeywordCase.Lower => text.ToLowerInvariant(),
                        _ => text,
                    },
                    TokenKind.LineComment => text.TrimEnd('\r', '\n'),
                    _ => text,
                });
                atLineStart = false;
                prevOpenedSubquery = opensSubquery;
                unary = token.Kind == TokenKind.Operator && text is "+" or "-"
                    && (prev is null or { Kind: TokenKind.Operator or TokenKind.Keyword } || prevText is "(" or ",");
                needBreak = token.Kind == TokenKind.LineComment;
                if (needBreak && commentBreakIndent is { } indent)
                {
                    lineIndent = indent;
                    commentBreakIndent = null;
                }

                if (isPunct && text == "(")
                {
                    levels.Push(opensSubquery
                        ? new Level(true, lineIndent + 1, lineIndent) { Subquery = true }
                        : new Level(false, level.Indent, lineIndent));
                }
                else if (isPunct && text == "," && level.InSelectList)
                {
                    if (next is { Kind: TokenKind.LineComment })
                    {
                        // keep the comment on the item's line; the break comes after it
                        commentBreakIndent = level.Indent + 1;
                    }
                    else
                    {
                        NewLine(level.Indent + 1);
                    }
                }

                prev = token;
            }

            return sb.ToString();
        }

        private string? WordOf(Token token) =>
            token.Kind is TokenKind.Keyword or TokenKind.Identifier ? token.Text(src).ToUpperInvariant() : null;

        private void BreakBefore(Level level, Token token, string? word, string? prevWord, string? nextWord, bool functionCall, bool opensSubquery)
        {
            var isClauseWord = level.Clause && word is not null && ClauseWords.Contains(word);
            if (level.AfterSelect)
            {
                // shortcut: only "TOP n" and "DISTINCT ON (...)" are recognised as select modifiers, "TOP (n)" and "WITH TIES" start the item list.
                var inline = word is "DISTINCT" or "ALL" or "TOP"
                    || word == "ON" && prevWord == "DISTINCT"
                    || token.Kind == TokenKind.Punctuation && src[token.Start] == '(' && !opensSubquery && prevWord is "TOP" or "ON"
                    || token.Kind == TokenKind.NumberLiteral && prevWord == "TOP";
                if (!inline && !isClauseWord)
                {
                    level.AfterSelect = false;
                    NewLine(level.Indent + 1);
                }
            }

            if (!level.Clause || word is null)
            {
                return;
            }

            var first = sb.Length == 0;
            if (isClauseWord && (word is not ("GROUP" or "ORDER") || nextWord == "BY") && !(word == "FROM" && prevWord == "DISTINCT"))
            {
                level.InSelectList = word == "SELECT";
                level.AfterSelect = word == "SELECT";
                level.JoinPending = false;
                if (!first)
                {
                    NewLine(level.Indent);
                }
            }
            else if (word == "WITH" && prevOpenedSubquery)
            {
                NewLine(level.Indent);
            }
            else if (word is "JOIN" or "APPLY" or "STRAIGHT_JOIN" || JoinModifiers.Contains(word))
            {
                var isModifier = JoinModifiers.Contains(word);
                if (functionCall && word is "LEFT" or "RIGHT")
                {
                    return;
                }

                level.InSelectList = false;
                level.AfterSelect = false;
                if (!first && !JoinModifiers.Contains(prevWord ?? string.Empty))
                {
                    NewLine(level.Indent);
                }

                level.JoinPending |= !isModifier;
            }
            else if (word == "ON" && level.JoinPending)
            {
                level.JoinPending = false;
                NewLine(level.Indent + 1);
            }
        }

        private void NewLine(int indent)
        {
            if (sb.Length == 0)
            {
                return;
            }

            sb.Append('\n');
            sb.Append(' ', indent * options.IndentSize);
            lineIndent = indent;
            atLineStart = true;
        }
    }
}
