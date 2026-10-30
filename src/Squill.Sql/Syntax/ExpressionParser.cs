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
    private const int Postfix = 9;

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
            core.Report(token.Start, token.Length, "Unexpected token");
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
            core.ReportMissing("Expected AND");
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
                "=" or "<>" or "!=" or "<" or ">" or "<=" or ">=" => (text, Comparison, 1),
                "||" => (text, Concat, 1),
                "+" or "-" => (text, Additive, 1),
                "*" or "/" or "%" => (text, Multiplicative, 1),
                "::" => (text, Postfix, 1),
                _ => null,
            };
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
            case "IN" or "BETWEEN" or "LIKE" or "ILIKE":
                return (core.TextOf(token).ToUpperInvariant(), Comparison, 1);
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
                foreach (var word in (ReadOnlySpan<string>)["IN", "BETWEEN", "LIKE", "ILIKE"])
                {
                    if (core.IsWord(next, word))
                    {
                        return ("NOT " + word, Comparison, 2);
                    }
                }

                return null;
            default:
                return null;
        }
    }

    private Expression ParsePrefix()
    {
        var token = core.Peek();
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
        core.ReportMissing("Expected expression");
        return new ErrorNode(core.LastEnd, 0);
    }

    /// <summary>Consumes a token that cannot start an expression and covers it with an error node.</summary>
    private ErrorNode Unexpected(Token token)
    {
        core.Next();
        core.Report(token.Start, token.Length, $"Unexpected '{core.TextOf(token)}'");
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
                core.Report(dot.Start, dot.Length, "Expected identifier after '.'");
                parts.Add(string.Empty);
                break;
            }
        }

        var length = core.LastEnd - first.Start;
        if (star)
        {
            return new StarExpr(string.Join('.', parts), first.Start, length);
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
        if (!core.IsPunct(core.Peek(), ')'))
        {
            do
            {
                args.Add(ParseExpression());
            }
            while (core.MatchPunct(','));
        }

        core.Expect(')');
        return new FunctionCall(name, args, distinct, start, core.LastEnd - start);
    }

    private Expression ParseParenthesized(bool asList)
    {
        var open = core.Peek();
        if (!core.MatchPunct('('))
        {
            core.ReportMissing("Expected '('");
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
                core.ReportMissing("Expected THEN");
            }

            var result = ParseExpression();
            whens.Add(new WhenClause(condition, result, whenStart, Math.Max(core.LastEnd, result.End) - whenStart));
        }

        if (whens.Count == 0)
        {
            core.ReportMissing("Expected WHEN");
        }

        var otherwise = core.Match("ELSE") ? ParseExpression() : null;
        if (!core.Match("END"))
        {
            core.ReportMissing("Expected END");
        }

        return new CaseExpr(operand, whens, otherwise, start, Math.Max(core.LastEnd, otherwise?.End ?? 0) - start);
    }

    private CastExpr ParseCast()
    {
        var start = core.Next().Start;
        core.Expect('(');
        var operand = ParseExpression();
        if (!core.Match("AS"))
        {
            core.ReportMissing("Expected AS");
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
            core.ReportMissing("Expected type name");
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

        return core.Text[first.Start..core.LastEnd];
    }

    private string NameOf(Token token) => core.NameOf(token);
}
