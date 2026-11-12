using System.Collections.Frozen;
using Squill.Core;
using Squill.Sql.Lexing;

namespace Squill.Sql.Syntax;

/// <summary>Pratt parser for SQL expressions that never throws and always returns a tree.</summary>
/// <param name="core">The token cursor to read from and report to.</param>
internal sealed class ExpressionParser(SqlParserCore core)
{
    private const int Or = 1;
    private const int And = 2;
    private const int Comparison = 4;
    private const int Concat = 5;
    private const int Additive = 6;
    private const int Multiplicative = 7;
    private const int Arrow = 8;
    private const int Postfix = 9;

    // Functions whose first argument is a date part written as a bare word: DATEADD(day, 1, x), EXTRACT(year FROM x).
    private static readonly FrozenSet<string> DatePartFunctions = new[]
    {
        "DATEADD", "DATEDIFF", "DATEDIFF_BIG", "DATEPART", "DATENAME", "DATETRUNC", "EXTRACT",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    // Functions whose argument list uses FROM or FOR as a separator: EXTRACT(year FROM x), SUBSTRING(s FROM 1 FOR 2).
    private static readonly FrozenSet<string> FromForFunctions = new[]
    {
        "EXTRACT", "SUBSTRING", "SUBSTR", "OVERLAY", "TRIM",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> IntervalUnits = new[]
    {
        "MICROSECOND", "SECOND", "MINUTE", "HOUR", "DAY", "WEEK", "MONTH", "QUARTER", "YEAR",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Keywords that end an expression (clause starts) or are operators, so never begin an operand.</summary>
    internal static readonly FrozenSet<string> Boundary = new[]
    {
        "SELECT", "FROM", "WHERE", "GROUP", "HAVING", "ORDER", "BY", "LIMIT", "OFFSET", "UNION", "INTERSECT", "EXCEPT",
        "JOIN", "INNER", "OUTER", "CROSS", "FULL", "NATURAL", "LEFT", "RIGHT", "ON", "USING", "SET", "VALUES", "INTO",
        "WHEN", "THEN", "ELSE", "END", "AS", "ASC", "DESC", "RETURNING", "WITH", "INSERT", "UPDATE", "DELETE", "CREATE",
        "DROP", "ALTER", "AND", "OR", "IN", "IS", "LIKE", "BETWEEN",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Parses a whole text as one expression; leftover tokens are reported.</summary>
    /// <param name="text">The SQL text.</param>
    /// <param name="dialect">The dialect that selects the tokenizer.</param>
    /// <param name="diagnostics">The problems found.</param>
    /// <returns>The expression tree.</returns>
    public static Expression ParseText(string text, DatabaseDialect dialect, out IReadOnlyList<ParseDiagnostic> diagnostics)
    {
        var core = new SqlParserCore(text, dialect);
        var expression = new ExpressionParser(core).ParseExpression();
        while (!core.AtEnd)
        {
            var token = core.Next();
            core.Report(token.Start, token.Length, "Unexpected token", SqlMessageCodes.UnexpectedToken);
        }

        diagnostics = core.Diagnostics;
        return expression;
    }

    /// <summary>Parses one expression at the cursor and stops before a token that cannot continue it.</summary>
    /// <returns>The expression tree; an <see cref="ErrorNode"/> where input is missing or invalid.</returns>
    public Expression ParseExpression() => ParseExpression(Or);

    /// <summary>Parses a single operand (literal, name, call, parenthesized expression) without any binary operator.</summary>
    /// <returns>The operand.</returns>
    public Expression ParseOperand() => ParsePrefix();

    private Expression ParseExpression(int minPrecedence)
    {
        var left = ParsePrefix();
        while (PeekInfix() is { } infix && infix.Precedence >= minPrecedence)
        {
            for (var i = 0; i < infix.Tokens; i++)
            {
                core.Next();
            }

            left = infix.Op switch
            {
                "::" => new CastExpr(left, ParseTypeName(multiWord: false), left.Start, core.LastEnd - left.Start),
                "[" => ParseSubscript(left),
                "COLLATE" => ParseCollate(left),
                "IN" or "NOT IN" => Binary(infix.Op, left, ParseParenthesized(asList: true)),
                "BETWEEN" or "NOT BETWEEN" => ParseBetween(infix.Op, left),
                "IS" or "IS NOT" or "IS DISTINCT FROM" or "IS NOT DISTINCT FROM" =>
                    Binary(infix.Op, left, ParseExpression(Concat)),
                _ => Binary(infix.Op, left, ParseExpression(infix.Precedence + 1)),
            };
        }

        return left;
    }

    private BinaryExpr Binary(string op, Expression left, Expression right) =>
        new(op, left, right, left.Start, Math.Max(core.LastEnd, right.End) - left.Start);

    private BinaryExpr ParseBetween(string op, Expression left)
    {
        var low = ParseExpression(Concat);
        if (!core.Match("AND"))
        {
            core.ReportMissing("Expected AND", SqlMessageCodes.ExpectedKeyword, "AND");
        }

        var high = ParseExpression(Concat);
        var range = Binary("AND", low, high);
        return Binary(op, left, range);
    }

    private (string Op, int Precedence, int Tokens)? PeekInfix()
    {
        var token = core.Peek();
        if (token.Kind == TokenKind.Operator)
        {
            var text = core.TextOf(token);
            return text switch
            {
                "=" or "==" or "<>" or "!=" or "<" or ">" or "<=" or ">=" or "<=>" => (text, Comparison, 1),
                "~" or "~*" or "!~" or "!~*" or "@>" or "<@" or "&&" => (text, Comparison, 1),
                "->" or "->>" or "#>" or "#>>" => (text, Arrow, 1),
                "@?" or "@@" or "?" or "?|" or "?&" => (text, Comparison, 1),
                "#-" => (text, Concat, 1),
                "&" or "|" or "^" or "<<" or ">>" => (text, Additive, 1),
                ":=" => (text, Or, 1),
                "||" => (text, Concat, 1),
                "+" or "-" => (text, Additive, 1),
                "*" or "/" or "%" => (text, Multiplicative, 1),
                "::" => (text, Postfix, 1),
                _ => null,
            };
        }

        if (IsBracket(token, '['))
        {
            return ("[", Postfix, 1);
        }

        if (token.Kind is not (TokenKind.Keyword or TokenKind.Identifier))
        {
            return null;
        }

        switch (core.TextOf(token).ToUpperInvariant())
        {
            case "OR" when token.Kind == TokenKind.Keyword:
                return ("OR", Or, 1);
            case "AND" when token.Kind == TokenKind.Keyword:
                return ("AND", And, 1);
            case "IN" or "BETWEEN" or "LIKE" or "ILIKE" or "REGEXP" or "RLIKE" or "GLOB":
                return (core.TextOf(token).ToUpperInvariant(), Comparison, 1);
            case "SIMILAR" when core.IsWord(core.Peek(1), "TO"):
                return ("SIMILAR TO", Comparison, 2);
            case "COLLATE":
                return ("COLLATE", Postfix, 1);
            case "IS" when token.Kind == TokenKind.Keyword:
                var op = "IS";
                var count = 1;
                if (core.IsWord(core.Peek(count), "NOT"))
                {
                    op += " NOT";
                    count++;
                }

                if (core.IsWord(core.Peek(count), "DISTINCT") && core.IsWord(core.Peek(count + 1), "FROM"))
                {
                    op += " DISTINCT FROM";
                    count += 2;
                }

                return (op, Comparison, count);
            case "NOT" when token.Kind == TokenKind.Keyword:
                var next = core.Peek(1);
                foreach (var word in (ReadOnlySpan<string>)["IN", "BETWEEN", "LIKE", "ILIKE", "REGEXP", "RLIKE", "GLOB"])
                {
                    if (core.IsWord(next, word))
                    {
                        return ("NOT " + word, Comparison, 2);
                    }
                }

                if (core.IsWord(next, "SIMILAR") && core.IsWord(core.Peek(2), "TO"))
                {
                    return ("NOT SIMILAR TO", Comparison, 3);
                }

                return null;
            default:
                return null;
        }
    }

    private Expression ParsePrefix()
    {
        var token = core.Peek();
        if (core.IsWord(token, "INTERVAL") && core.Peek(1).Kind is TokenKind.NumberLiteral or TokenKind.StringLiteral)
        {
            return ParseInterval();
        }

        switch (token.Kind)
        {
            case TokenKind.NumberLiteral:
                return Literal(LiteralKind.Number);
            case TokenKind.StringLiteral:
                return Literal(LiteralKind.Text);
            case TokenKind.Parameter:
                core.Next();
                return new ParameterExpr(core.TextOf(token), token.Start, token.Length);
            case TokenKind.Identifier or TokenKind.QuotedIdentifier:
                return ParseNameOrCall();
            case TokenKind.Operator:
                return ParseOperatorPrefix(token);
            case TokenKind.Punctuation:
                if (core.IsPunct(token, '('))
                {
                    return ParseParenthesized(asList: false);
                }

                return core.IsPunct(token, '.') ? Unexpected(token) : Missing();
            case TokenKind.Keyword:
                return ParseKeywordPrefix(token);
            case TokenKind.EndOfFile:
                return Missing();
            default:
                return Unexpected(token);
        }
    }

    private Expression ParseOperatorPrefix(Token token)
    {
        var text = core.TextOf(token);
        if (text is "-" or "+")
        {
            core.Next();
            var operand = ParseExpression(Postfix);
            return new UnaryExpr(text, operand, token.Start, Math.Max(core.LastEnd, operand.End) - token.Start);
        }

        if (text == "*")
        {
            core.Next();
            return new StarExpr(null, token.Start, token.Length);
        }

        return Unexpected(token);
    }

    private Expression ParseKeywordPrefix(Token token)
    {
        switch (core.TextOf(token).ToUpperInvariant())
        {
            case "TRUE" or "FALSE":
                return Literal(LiteralKind.Boolean);
            case "NULL":
                return Literal(LiteralKind.Null);
            case "NOT":
                core.Next();
                var operand = ParseExpression(Comparison);
                return new UnaryExpr("NOT", operand, token.Start, Math.Max(core.LastEnd, operand.End) - token.Start);
            case "EXISTS":
                core.Next();
                var query = core.IsPunct(core.Peek(), '(') ? ParseParenthesized(asList: false) : Missing();
                return new UnaryExpr("EXISTS", query, token.Start, Math.Max(core.LastEnd, query.End) - token.Start);
            case "CASE":
                return ParseCase();
            case "CAST":
                return ParseCast();
            default:
                return IsBoundary() ? Missing() : ParseNameOrCall();
        }
    }

    /// <summary>Tests whether the current keyword ends the expression; <c>LEFT(</c> and <c>RIGHT(</c> are functions.</summary>
    private bool IsBoundary()
    {
        var token = core.Peek();
        if (token.Kind != TokenKind.Keyword || !Boundary.Contains(core.TextOf(token)))
        {
            return false;
        }

        return !(core.IsPunct(core.Peek(1), '(') && (core.IsWord(token, "LEFT") || core.IsWord(token, "RIGHT")));
    }

    /// <summary>Reports missing input and returns an empty node without consuming a token.</summary>
    private ErrorNode Missing()
    {
        core.ReportMissing("Expected expression", SqlMessageCodes.ExpectedExpression);
        return new ErrorNode(core.LastEnd, 0);
    }

    /// <summary>Consumes a token that cannot start an expression and covers it with an error node.</summary>
    private ErrorNode Unexpected(Token token)
    {
        core.Next();
        core.Report(token.Start, token.Length, $"Unexpected '{core.TextOf(token)}'", SqlMessageCodes.Unexpected, core.TextOf(token));
        return new ErrorNode(token.Start, token.Length);
    }

    private LiteralExpr Literal(LiteralKind kind)
    {
        var token = core.Next();
        return new LiteralExpr(kind, token.Start, token.Length);
    }

    private Expression ParseNameOrCall()
    {
        var first = core.Next();
        var parts = new List<string> { NameOf(first) };
        var star = false;
        while (core.IsPunct(core.Peek(), '.'))
        {
            var dot = core.Next();
            var next = core.Peek();
            if (next.Kind is TokenKind.Identifier or TokenKind.QuotedIdentifier
                || (next.Kind == TokenKind.Keyword && !IsBoundary()))
            {
                core.Next();
                parts.Add(NameOf(next));
            }
            else if (core.IsOperator(next, "*"))
            {
                core.Next();
                star = true;
                break;
            }
            else
            {
                core.Report(dot.Start, dot.Length, "Expected identifier after '.'", SqlMessageCodes.ExpectedIdentifierAfterDot);
                parts.Add(string.Empty);
                break;
            }
        }

        var length = core.LastEnd - first.Start;
        if (star)
        {
            return new StarExpr(string.Join('.', parts), first.Start, length);
        }

        if (parts.Count == 1 && core.IsWord(first, "ARRAY") && IsBracket(core.Peek(), '['))
        {
            return ParseArrayConstructor(first.Start);
        }

        if (parts.Count == 1 && core.IsWord(first, "TRY_CAST") && core.IsPunct(core.Peek(), '('))
        {
            return ParseCastBody(first.Start);
        }

        if (parts[^1].Length > 0 && core.IsPunct(core.Peek(), '('))
        {
            return ParseCall(string.Join('.', parts), first.Start);
        }

        return new ColumnRef(parts.Count > 1 ? string.Join('.', parts[..^1]) : null, parts[^1], first.Start, length);
    }

    private FunctionCall ParseCall(string name, int start)
    {
        core.Next();
        var distinct = core.Match("DISTINCT");
        var args = new List<Expression>();
        var extra = new List<Expression>();
        if (!core.IsPunct(core.Peek(), ')'))
        {
            var first = true;
            do
            {
                args.Add(first ? ParseFirstArgument(name) : ParseExpression());
                first = false;
                while (IsSeparatorWord(name))
                {
                    core.Next();
                    args.Add(ParseExpression());
                }
            }
            while (core.MatchPunct(','));
        }

        if (core.Match("ORDER"))
        {
            ExpectBy();
            ParseOrderList(extra);
        }

        core.Expect(')');
        ParseCallTail(extra);
        return new FunctionCall(name, args, distinct, start, core.LastEnd - start, extra.Count == 0 ? null : extra);
    }

    // First argument: a bare date part, a T-SQL CONVERT type or a TRIM direction is not an expression.
    private Expression ParseFirstArgument(string name)
    {
        var token = core.Peek();
        var bare = token.Kind is TokenKind.Identifier or TokenKind.Keyword;
        if (bare && DatePartFunctions.Contains(name) && (core.IsPunct(core.Peek(1), ',') || core.IsWord(core.Peek(1), "FROM")))
        {
            core.Next();
            return new LiteralExpr(LiteralKind.Text, token.Start, token.Length);
        }

        if (core.Dialect == DatabaseDialect.SqlServer && (name.Equals("CONVERT", StringComparison.OrdinalIgnoreCase) || name.Equals("TRY_CONVERT", StringComparison.OrdinalIgnoreCase)))
        {
            ParseTypeName(multiWord: true);
            return new LiteralExpr(LiteralKind.Text, token.Start, core.LastEnd - token.Start);
        }

        if (bare && name.Equals("TRIM", StringComparison.OrdinalIgnoreCase) && (core.IsWord(token, "BOTH") || core.IsWord(token, "LEADING") || core.IsWord(token, "TRAILING")))
        {
            core.Next();
            return core.IsWord(core.Peek(), "FROM") ? new LiteralExpr(LiteralKind.Text, token.Start, token.Length) : ParseExpression();
        }

        return ParseExpression();
    }

    private bool IsSeparatorWord(string name) =>
        (FromForFunctions.Contains(name) && (core.IsWord(core.Peek(), "FROM") || core.IsWord(core.Peek(), "FOR")))
        || (name.Equals("GROUP_CONCAT", StringComparison.OrdinalIgnoreCase) && core.IsWord(core.Peek(), "SEPARATOR"));

    // Clauses that follow a call's closing parenthesis: FILTER (WHERE ...), WITHIN GROUP (ORDER BY ...), OVER (...) or OVER name.
    private void ParseCallTail(List<Expression> extra)
    {
        while (true)
        {
            if (core.IsWord(core.Peek(), "FILTER") && core.IsPunct(core.Peek(1), '('))
            {
                core.Next();
                core.Next();
                if (core.Match("WHERE"))
                {
                    extra.Add(ParseExpression());
                }
                else
                {
                    core.ReportMissing("Expected WHERE", SqlMessageCodes.ExpectedKeyword, "WHERE");
                }

                core.Expect(')');
            }
            else if (core.IsWord(core.Peek(), "WITHIN") && core.IsWord(core.Peek(1), "GROUP") && core.IsPunct(core.Peek(2), '('))
            {
                core.Next();
                core.Next();
                core.Next();
                if (core.Match("ORDER"))
                {
                    ExpectBy();
                    ParseOrderList(extra);
                }

                core.Expect(')');
            }
            else if (core.IsWord(core.Peek(), "OVER"))
            {
                core.Next();
                if (core.MatchPunct('('))
                {
                    ParseWindowSpec(extra);
                }
                else if (core.Peek().Kind is TokenKind.Identifier or TokenKind.QuotedIdentifier)
                {
                    core.Next();
                }
                else
                {
                    core.ReportMissing("Expected '('", SqlMessageCodes.ExpectedChar, "(");
                }
            }
            else
            {
                return;
            }
        }
    }

    // After the opening parenthesis of OVER (...): [name] [PARTITION BY ...] [ORDER BY ...] [frame]; the frame is skipped.
    private void ParseWindowSpec(List<Expression> extra)
    {
        var token = core.Peek();
        if (token.Kind is TokenKind.Identifier or TokenKind.QuotedIdentifier
            && !core.IsWord(token, "PARTITION") && !core.IsWord(token, "ROWS") && !core.IsWord(token, "RANGE") && !core.IsWord(token, "GROUPS"))
        {
            core.Next();
        }

        if (core.Match("PARTITION"))
        {
            ExpectBy();
            do
            {
                extra.Add(ParseExpression());
            }
            while (core.MatchPunct(','));
        }

        if (core.Match("ORDER"))
        {
            ExpectBy();
            ParseOrderList(extra);
        }

        core.SkipToCloseParen();
        core.Expect(')');
    }

    private void ParseOrderList(List<Expression> list)
    {
        do
        {
            list.Add(ParseExpression());
            if (!core.Match("DESC"))
            {
                core.Match("ASC");
            }

            if (core.Match("NULLS") && !core.Match("FIRST"))
            {
                core.Match("LAST");
            }
        }
        while (core.MatchPunct(','));
    }

    private void ExpectBy()
    {
        if (!core.Match("BY"))
        {
            core.ReportMissing("Expected BY", SqlMessageCodes.ExpectedKeyword, "BY");
        }
    }

    private bool IsBracket(Token token, char c) =>
        token.Kind == TokenKind.Unknown && token.Length == 1 && core.Text[token.Start] == c;

    // ARRAY[a, b, ...] with the cursor on '['; nested brackets are plain array constructors without the keyword.
    private FunctionCall ParseArrayConstructor(int start)
    {
        core.Next();
        var items = new List<Expression>();
        if (!IsBracket(core.Peek(), ']'))
        {
            do
            {
                items.Add(IsBracket(core.Peek(), '[') ? ParseArrayConstructor(core.Peek().Start) : ParseExpression());
            }
            while (core.MatchPunct(','));
        }

        ExpectBracket(']');
        return new FunctionCall("ARRAY", items, false, start, core.LastEnd - start);
    }

    private void ExpectBracket(char c)
    {
        if (IsBracket(core.Peek(), c))
        {
            core.Next();
        }
        else
        {
            core.ReportMissing($"Expected '{c}'", SqlMessageCodes.ExpectedChar, c.ToString());
        }
    }

    // expr[index] with the cursor just after '['.
    private BinaryExpr ParseSubscript(Expression left)
    {
        var index = ParseExpression();
        ExpectBracket(']');
        return Binary("[]", left, index);
    }

    private BinaryExpr ParseCollate(Expression left)
    {
        var token = core.Peek();
        if (token.Kind is TokenKind.Identifier or TokenKind.QuotedIdentifier or TokenKind.Keyword or TokenKind.StringLiteral)
        {
            core.Next();
            while (core.IsPunct(core.Peek(), '.') && core.Peek(1).Kind is TokenKind.Identifier or TokenKind.QuotedIdentifier)
            {
                core.Next();
                core.Next();
            }

            return Binary("COLLATE", left, new LiteralExpr(LiteralKind.Text, token.Start, core.LastEnd - token.Start));
        }

        core.ReportMissing("Expected collation name", SqlMessageCodes.ExpectedExpression);
        return Binary("COLLATE", left, new ErrorNode(core.LastEnd, 0));
    }

    // INTERVAL '1 day' (PostgreSQL) or INTERVAL 30 DAY (MySQL).
    private FunctionCall ParseInterval()
    {
        var start = core.Next().Start;
        var value = ParseExpression(Postfix);
        var unit = core.Peek();
        if (unit.Kind is TokenKind.Identifier or TokenKind.Keyword && core.TextOf(unit).Split('_').All(IsIntervalUnit))
        {
            core.Next();
        }

        return new FunctionCall("INTERVAL", [value], false, start, core.LastEnd - start);
    }

    private static bool IsIntervalUnit(string word) =>
        IntervalUnits.Contains(word.EndsWith('S') || word.EndsWith('s') ? word[..^1] : word);

    private Expression ParseParenthesized(bool asList)
    {
        var open = core.Peek();
        if (!core.MatchPunct('('))
        {
            core.ReportMissing("Expected '('", SqlMessageCodes.ExpectedChar, "(");
            return new ErrorNode(core.LastEnd, 0);
        }

        if (core.IsWord(core.Peek(), "SELECT") || core.IsWord(core.Peek(), "WITH"))
        {
            var query = new SelectParser(core).ParseParenthesizedQuery();
            return new SubqueryExpr(query, open.Start, core.LastEnd - open.Start);
        }

        var items = new List<Expression> { ParseExpression() };
        while (core.MatchPunct(','))
        {
            items.Add(ParseExpression());
        }

        core.Expect(')');
        var length = core.LastEnd - open.Start;
        return items.Count == 1 && !asList ? new ParenExpr(items[0], open.Start, length) : new ListExpr(items, open.Start, length);
    }

    private CaseExpr ParseCase()
    {
        var start = core.Next().Start;
        Expression? operand = null;
        if (!core.IsWord(core.Peek(), "WHEN") && !core.IsWord(core.Peek(), "END") && core.Peek().Kind != TokenKind.EndOfFile)
        {
            operand = ParseExpression();
        }

        var whens = new List<WhenClause>();
        while (core.IsWord(core.Peek(), "WHEN"))
        {
            var whenStart = core.Next().Start;
            var condition = ParseExpression();
            if (!core.Match("THEN"))
            {
                core.ReportMissing("Expected THEN", SqlMessageCodes.ExpectedKeyword, "THEN");
            }

            var result = ParseExpression();
            whens.Add(new WhenClause(condition, result, whenStart, Math.Max(core.LastEnd, result.End) - whenStart));
        }

        if (whens.Count == 0)
        {
            core.ReportMissing("Expected WHEN", SqlMessageCodes.ExpectedKeyword, "WHEN");
        }

        var otherwise = core.Match("ELSE") ? ParseExpression() : null;
        if (!core.Match("END"))
        {
            core.ReportMissing("Expected END", SqlMessageCodes.ExpectedKeyword, "END");
        }

        return new CaseExpr(operand, whens, otherwise, start, Math.Max(core.LastEnd, otherwise?.End ?? 0) - start);
    }

    private CastExpr ParseCast() => ParseCastBody(core.Next().Start);

    private CastExpr ParseCastBody(int start)
    {
        core.Expect('(');
        var operand = ParseExpression();
        if (!core.Match("AS"))
        {
            core.ReportMissing("Expected AS", SqlMessageCodes.ExpectedKeyword, "AS");
        }

        var type = ParseTypeName(multiWord: true);
        core.Expect(')');
        return new CastExpr(operand, type, start, core.LastEnd - start);
    }

    /// <summary>Reads a type name; <paramref name="multiWord"/> accepts words such as <c>double precision</c>.</summary>
    private string ParseTypeName(bool multiWord)
    {
        var first = core.Peek();
        if (first.Kind is not (TokenKind.Identifier or TokenKind.QuotedIdentifier or TokenKind.Keyword) || IsBoundary())
        {
            core.ReportMissing("Expected type name", SqlMessageCodes.ExpectedTypeName);
            return string.Empty;
        }

        core.Next();
        while (core.IsPunct(core.Peek(), '.') && core.Peek(1).Kind is TokenKind.Identifier or TokenKind.QuotedIdentifier)
        {
            core.Next();
            core.Next();
        }

        while (multiWord && core.Peek().Kind is TokenKind.Identifier or TokenKind.Keyword)
        {
            core.Next();
        }

        if (core.MatchPunct('('))
        {
            core.SkipToCloseParen();
            core.Expect(')');
        }

        // PostgreSQL array types: integer[] or text[][]
        while (IsBracket(core.Peek(), '[') && IsBracket(core.Peek(1), ']'))
        {
            core.Next();
            core.Next();
        }

        return core.Text[first.Start..core.LastEnd];
    }

    private string NameOf(Token token) => core.NameOf(token);
}
