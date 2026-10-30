using Squill.Core;
using Squill.Sql.Lexing;

namespace Squill.Sql.Statements;

/// <summary>Splits a script into statements and finds the statement under a caret.</summary>
public static class SqlStatementSplitter
{
    /// <summary>Splits a script on <c>;</c> tokens, honouring T-SQL <c>GO</c>, MySQL <c>DELIMITER</c> and compound bodies.</summary>
    /// <param name="text">The script.</param>
    /// <param name="dialect">The database engine, which selects the tokenizer and the batch directives.</param>
    /// <returns>Non-empty statement spans in text order; comment-only and empty statements are dropped.</returns>
    public static IReadOnlyList<SqlStatementSpan> Split(string text, DatabaseDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(text);
        var tokens = SqlTokenizers.For(dialect).Tokenize(text);
        var result = new List<SqlStatementSpan>();
        var delimiter = ";";
        int start = -1, end = 0, count = 0, depth = 0;
        bool isCreate = false, sawObject = false, bodyStarted = false;

        void Flush()
        {
            if (count > 0)
            {
                result.Add(new SqlStatementSpan(start, end - start));
            }

            start = -1;
            count = 0;
            depth = 0;
            isCreate = sawObject = bodyStarted = false;
        }

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (IsTrivia(token))
            {
                continue;
            }

            if (IsDirective(text, token, dialect, out var lineEnd, out var newDelimiter))
            {
                Flush();
                if (newDelimiter is not null)
                {
                    delimiter = newDelimiter;
                }

                while (i + 1 < tokens.Count && tokens[i + 1].Start < lineEnd)
                {
                    i++;
                }

                continue;
            }

            if (delimiter == ";")
            {
                if (token.Kind == TokenKind.Punctuation && text[token.Start] == ';' && depth == 0)
                {
                    if (count > 0)
                    {
                        end = token.End;
                    }

                    Flush();
                    continue;
                }
            }
            else if (string.CompareOrdinal(text, token.Start, delimiter, 0, delimiter.Length) == 0)
            {
                // shortcut: a custom delimiter glued to an identifier (END$$) is not split, only token-aligned ones are.
                Flush();
                var delimiterEnd = token.Start + delimiter.Length;
                while (i + 1 < tokens.Count && tokens[i + 1].Start < delimiterEnd)
                {
                    i++;
                }

                continue;
            }

            if (start < 0)
            {
                start = token.Start;
            }

            end = token.End;
            count++;
            if (delimiter != ";" || !IsWord(token))
            {
                continue;
            }

            if (count == 1)
            {
                isCreate = WordIs(text, token, "CREATE");
            }
            else if (isCreate && depth == 0 && !bodyStarted)
            {
                if (WordIs(text, token, "TRIGGER") || WordIs(text, token, "PROCEDURE") || WordIs(text, token, "FUNCTION"))
                {
                    sawObject = true;
                }
                else if (WordIs(text, token, "BEGIN"))
                {
                    bodyStarted = true;
                    depth = sawObject ? 1 : 0;
                }
            }
            else if (depth > 0)
            {
                if (WordIs(text, token, "BEGIN"))
                {
                    if (!NextWordIs(text, tokens, i, "TRAN", "TRANSACTION", "DISTRIBUTED"))
                    {
                        depth++;
                    }
                }
                else if (WordIs(text, token, "CASE"))
                {
                    depth++;
                }
                else if (WordIs(text, token, "END") && !NextWordIs(text, tokens, i, "IF", "LOOP", "WHILE", "REPEAT"))
                {
                    depth--;
                }
            }
        }

        Flush();
        return result;
    }

    /// <summary>Finds the statement a caret belongs to.</summary>
    /// <param name="text">The script.</param>
    /// <param name="offset">Caret offset; clamped to the text.</param>
    /// <param name="dialect">The database engine.</param>
    /// <returns>The statement containing the caret, the closest previous one when the caret sits between statements (the first one when it precedes them all), or <see langword="null"/> when the text has no statement.</returns>
    public static SqlStatementSpan? GetStatementAt(string text, int offset, DatabaseDialect dialect)
    {
        var statements = Split(text, dialect);
        if (statements.Count == 0)
        {
            return null;
        }

        offset = Math.Clamp(offset, 0, text.Length);
        var found = statements[0];
        foreach (var statement in statements)
        {
            if (statement.Start > offset)
            {
                break;
            }

            found = statement;
        }

        return found;
    }

    private static bool IsTrivia(Token token) =>
        token.Kind is TokenKind.Whitespace or TokenKind.LineComment or TokenKind.BlockComment;

    private static bool IsWord(Token token) => token.Kind is TokenKind.Keyword or TokenKind.Identifier;

    private static bool WordIs(string text, Token token, string word) =>
        token.Length == word.Length && string.Compare(text, token.Start, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) == 0;

    private static bool NextWordIs(string text, IReadOnlyList<Token> tokens, int index, params ReadOnlySpan<string> words)
    {
        for (var i = index + 1; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (IsTrivia(token))
            {
                continue;
            }

            if (IsWord(token))
            {
                foreach (var word in words)
                {
                    if (WordIs(text, token, word))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        return false;
    }

    // Recognizes a T-SQL "GO [count]" line or a MySQL "DELIMITER x" line starting at the token.
    private static bool IsDirective(string text, Token token, DatabaseDialect dialect, out int lineEnd, out string? newDelimiter)
    {
        lineEnd = 0;
        newDelimiter = null;
        var isGo = dialect == DatabaseDialect.SqlServer && IsWord(token) && WordIs(text, token, "GO");
        var isDelimiter = dialect == DatabaseDialect.MySql && IsWord(token) && WordIs(text, token, "DELIMITER");
        if (!isGo && !isDelimiter)
        {
            return false;
        }

        for (var p = token.Start - 1; p >= 0 && text[p] != '\n'; p--)
        {
            if (!char.IsWhiteSpace(text[p]))
            {
                return false;
            }
        }

        lineEnd = text.IndexOf('\n', token.End);
        if (lineEnd < 0)
        {
            lineEnd = text.Length;
        }

        var rest = text.AsSpan(token.End, lineEnd - token.End).Trim();
        if (isGo)
        {
            foreach (var c in rest)
            {
                if (!char.IsAsciiDigit(c))
                {
                    return false;
                }
            }

            return true;
        }

        if (rest.IsEmpty)
        {
            return false;
        }

        newDelimiter = rest.ToString();
        return true;
    }
}
