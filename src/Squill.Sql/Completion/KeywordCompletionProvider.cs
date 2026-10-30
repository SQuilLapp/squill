using Squill.Core;
using Squill.Sql.Analysis;
using Squill.Sql.Lexing;
using Squill.Sql.Syntax;

namespace Squill.Sql.Completion;

/// <summary>Proposes the keywords that are valid in the clause at the caret.</summary>
public sealed class KeywordCompletionProvider : ISqlCompletionProvider
{
    private static readonly string[] Expression =
        ["CASE", "NOT", "NULL", "TRUE", "FALSE", "EXISTS", "CAST", "AND", "OR", "IN", "IS NULL", "IS NOT NULL", "LIKE", "BETWEEN"];

    private static readonly string[] Joins =
        ["JOIN", "INNER JOIN", "LEFT JOIN", "RIGHT JOIN", "FULL JOIN", "CROSS JOIN"];

    private static readonly string[] SetOperators = ["UNION", "UNION ALL", "EXCEPT", "INTERSECT"];

    /// <inheritdoc />
    public IReadOnlyList<CompletionItem> GetCompletions(SqlRequest request)
    {
        var text = request.Text;
        var offset = Math.Clamp(request.Offset, 0, text.Length);
        var start = offset;
        while (start > 0 && IsWordChar(text[start - 1]))
        {
            start--;
        }

        var range = new TextRange(start, offset - start);
        var previous = Previous(text[..start], request.Dialect);
        var scope = SqlScopeResolver.Resolve(SqlParser.Parse(text, request.Dialect), offset);
        var words = Words(scope.Clause, previous, request.Dialect);
        return [.. words.Distinct().Select(w => new CompletionItem(w, CompletionKind.Keyword, w, null, null, range))];
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '$';

    /// <summary>Gets the last token before the prefix that is neither blank nor a comment.</summary>
    private static (TokenKind Kind, string Text)? Previous(string before, DatabaseDialect dialect)
    {
        var tokens = SqlTokenizers.For(dialect).Tokenize(before);
        for (var i = tokens.Count - 1; i >= 0; i--)
        {
            if (tokens[i].Kind is not (TokenKind.Whitespace or TokenKind.LineComment or TokenKind.BlockComment))
            {
                return (tokens[i].Kind, tokens[i].Text(before));
            }
        }

        return null;
    }

    private static List<string> Words(ClauseKind clause, (TokenKind Kind, string Text)? previous, DatabaseDialect dialect)
    {
        var words = new List<string>();
        if (previous is null || previous.Value.Text == ";")
        {
            words.AddRange(["SELECT", "INSERT", "UPDATE", "DELETE", "CREATE", "ALTER", "DROP", "WITH", "BEGIN", "COMMIT", "ROLLBACK"]);
            words.AddRange(dialect switch
            {
                DatabaseDialect.PostgreSql => ["EXPLAIN", "TRUNCATE", "VALUES"],
                DatabaseDialect.MySql => ["REPLACE", "SHOW", "USE", "TRUNCATE"],
                DatabaseDialect.SqlServer => ["EXEC", "MERGE", "USE", "DECLARE", "TRUNCATE"],
                _ => ["REPLACE", "PRAGMA", "VACUUM"],
            });
            return words;
        }

        // a source (table, alias, closing parenthesis) just ended, so the next word continues the clause
        var afterSource = previous.Value.Kind is TokenKind.Identifier or TokenKind.QuotedIdentifier
            || previous.Value.Text == ")";
        var limit = dialect == DatabaseDialect.SqlServer ? "OFFSET" : "LIMIT";
        switch (clause)
        {
            case ClauseKind.Select:
                words.AddRange(["DISTINCT", "ALL", "CASE", "NOT", "NULL", "TRUE", "FALSE", "EXISTS", "CAST", "AS", "FROM"]);
                if (dialect == DatabaseDialect.SqlServer)
                {
                    words.Add("TOP");
                }

                break;
            case ClauseKind.From when afterSource:
                words.AddRange(["AS", .. Joins, "WHERE", "GROUP BY", "ORDER BY", "HAVING", .. SetOperators, limit]);
                break;
            case ClauseKind.From when dialect == DatabaseDialect.PostgreSql:
                words.Add("LATERAL");
                break;
            case ClauseKind.Join when afterSource:
                words.AddRange(["ON", "USING", "AS", .. Joins, "WHERE"]);
                break;
            case ClauseKind.Join when dialect == DatabaseDialect.PostgreSql:
                words.Add("LATERAL");
                break;
            case ClauseKind.On:
                words.AddRange([.. Expression, .. Joins, "WHERE"]);
                break;
            case ClauseKind.Where:
                words.AddRange([.. Expression, "GROUP BY", "ORDER BY", .. SetOperators, limit]);
                break;
            case ClauseKind.GroupBy:
                words.AddRange(["HAVING", "ORDER BY", .. SetOperators, limit]);
                break;
            case ClauseKind.Having:
                words.AddRange([.. Expression, "ORDER BY", .. SetOperators, limit]);
                break;
            case ClauseKind.OrderBy:
                words.AddRange(["ASC", "DESC", limit, .. SetOperators]);
                if (dialect == DatabaseDialect.PostgreSql)
                {
                    words.AddRange(["NULLS FIRST", "NULLS LAST"]);
                }

                break;
            case ClauseKind.Set:
                words.AddRange(["NULL", "DEFAULT", "CASE", "WHERE"]);
                break;
            case ClauseKind.Values:
                words.AddRange(["NULL", "DEFAULT", "TRUE", "FALSE"]);
                break;
            case ClauseKind.Other:
                words.AddRange(["INTO", "VALUES", "SELECT", "SET", "FROM", "WHERE"]);
                break;
        }

        if (dialect == DatabaseDialect.PostgreSql && clause is ClauseKind.Set or ClauseKind.Values or ClauseKind.Other)
        {
            words.Add("RETURNING");
        }

        return words;
    }
}
