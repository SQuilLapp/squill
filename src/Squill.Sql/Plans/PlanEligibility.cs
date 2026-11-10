using Squill.Core;
using Squill.Core.Plans;
using Squill.Sql.Lexing;

namespace Squill.Sql.Plans;

/// <summary>Whether a statement may be explained or analyzed, with the reason code when it may not.</summary>
/// <param name="CanExplain">An estimated plan can be requested (the statement is not executed).</param>
/// <param name="CanAnalyze">An actual plan can be requested (the statement is executed).</param>
/// <param name="ExplainReasonCode">A <see cref="PlanMessageCodes"/> reason when <paramref name="CanExplain"/> is false.</param>
/// <param name="AnalyzeReasonCode">A <see cref="PlanMessageCodes"/> reason when <paramref name="CanAnalyze"/> is false.</param>
public sealed record PlanEligibility(bool CanExplain, bool CanAnalyze, string? ExplainReasonCode, string? AnalyzeReasonCode)
{
    /// <summary>Decides what may be done with a single statement.</summary>
    /// <param name="statement">One statement; if several are present only the first is evaluated.</param>
    /// <param name="dialect">The database engine, which selects the tokenizer and the explainable keywords.</param>
    /// <returns>The decision and, for each refused action, its reason code.</returns>
    public static PlanEligibility Evaluate(string statement, DatabaseDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(statement);
        var words = new List<string>();
        var kinds = new List<TokenKind>();
        foreach (var token in SqlTokenizers.For(dialect).Tokenize(statement))
        {
            if (token.Kind is TokenKind.Whitespace or TokenKind.LineComment or TokenKind.BlockComment)
            {
                continue;
            }

            var text = token.Text(statement);
            if (token.Kind == TokenKind.Punctuation && text == ";")
            {
                if (words.Count > 0)
                {
                    break;
                }

                continue;
            }

            kinds.Add(token.Kind);
            words.Add(token.Kind is TokenKind.Keyword or TokenKind.Identifier ? text.ToUpperInvariant() : text);
        }

        var first = 0;
        while (first < words.Count && words[first] == "(")
        {
            first++;
        }

        if (first >= words.Count)
        {
            return Refused(PlanMessageCodes.ReasonEmpty);
        }

        var head = words[first];
        if (head == "EXPLAIN" || (head == "SET" && first + 1 < words.Count && words[first + 1].StartsWith("SHOWPLAN", StringComparison.Ordinal)))
        {
            return Refused(PlanMessageCodes.ReasonAlreadyExplain);
        }

        if (!IsExplainable(head, dialect))
        {
            return Refused(PlanMessageCodes.ReasonNotExplainable);
        }

        if (dialect == DatabaseDialect.Sqlite)
        {
            return new PlanEligibility(true, false, null, PlanMessageCodes.ReasonNoAnalyze);
        }

        var readOnly = head is "SELECT" or "VALUES" or "TABLE" or "WITH" && !WritesData(words, kinds);
        return readOnly
            ? new PlanEligibility(true, true, null, null)
            : new PlanEligibility(true, false, null, PlanMessageCodes.ReasonWrites);
    }

    private static PlanEligibility Refused(string code) => new(false, false, code, code);

    private static bool IsExplainable(string head, DatabaseDialect dialect) => head switch
    {
        "SELECT" or "WITH" or "INSERT" or "UPDATE" or "DELETE" => dialect is DatabaseDialect.PostgreSql or DatabaseDialect.MySql or DatabaseDialect.SqlServer or DatabaseDialect.Sqlite,
        "VALUES" => dialect is DatabaseDialect.PostgreSql or DatabaseDialect.MySql or DatabaseDialect.Sqlite,
        "TABLE" => dialect is DatabaseDialect.PostgreSql or DatabaseDialect.MySql,
        "MERGE" => dialect is DatabaseDialect.PostgreSql or DatabaseDialect.SqlServer,
        "REPLACE" => dialect is DatabaseDialect.MySql or DatabaseDialect.Sqlite,
        _ => false,
    };

    // INTO catches SELECT ... INTO (creates a table); UPDATE right after FOR [NO KEY] is a lock clause, not a write.
    private static bool WritesData(List<string> words, List<TokenKind> kinds)
    {
        for (var i = 0; i < words.Count; i++)
        {
            if (kinds[i] != TokenKind.Keyword)
            {
                continue;
            }

            switch (words[i])
            {
                case "INTO" or "DELETE" or "MERGE":
                    return true;
                case "UPDATE" when i == 0 || words[i - 1] is not ("FOR" or "KEY"):
                    return true;
                // MySQL INSERT(str, pos, len, new) is a string function.
                case "INSERT" when i + 1 >= words.Count || words[i + 1] != "(":
                    return true;
            }
        }

        return false;
    }
}
