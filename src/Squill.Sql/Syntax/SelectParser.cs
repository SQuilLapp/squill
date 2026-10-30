using Squill.Core;
using Squill.Sql.Lexing;

namespace Squill.Sql.Syntax;

/// <summary>Recursive-descent parser for queries that never throws and always returns a tree.</summary>
/// <param name="core">The token cursor to read from and report to.</param>
internal sealed class SelectParser(SqlParserCore core)
{
    private readonly ExpressionParser _expressions = new(core);

    /// <summary>Parses the rest of the cursor as one statement; anything but a query becomes an <see cref="UnknownStatement"/>.</summary>
    /// <returns>The statement node.</returns>
    public Statement ParseStatement()
    {
        var first = core.Peek();
        if (!(core.IsWord(first, "SELECT") || core.IsWord(first, "WITH") || core.IsPunct(first, '(')))
        {
            SkipRest();
            return new UnknownStatement(first.Start, Math.Max(0, core.LastEnd - first.Start));
        }

        var result = ParseQuery();
        if (!core.AtEnd)
        {
            var extra = core.Peek();
            core.Report(extra.Start, extra.Length, $"Unexpected '{core.TextOf(extra)}'");
            SkipRest();
            result = result with { Length = core.LastEnd - result.Start };
        }

        return result;
    }

    /// <summary>Parses a query after its opening parenthesis and consumes the closing one.</summary>
    /// <returns>The query node.</returns>
    public Statement ParseParenthesizedQuery()
    {
        var query = ParseQuery();
        if (!core.IsPunct(core.Peek(), ')') && !core.AtEnd)
        {
            var extra = core.Peek();
            core.Report(extra.Start, extra.Length, $"Unexpected '{core.TextOf(extra)}'");
            core.SkipToCloseParen();
        }

        core.Expect(')');
        return query;
    }

    private void SkipRest()
    {
        while (!core.AtEnd)
        {
            core.Next();
        }
    }

    private Statement ParseQuery()
    {
        var start = core.Peek().Start;
        var ctes = new List<CommonTableExpr>();
        if (core.Match("WITH"))
        {
            core.Match("RECURSIVE");
            do
            {
                ctes.Add(ParseCte());
            }
            while (core.MatchPunct(','));
        }

        var body = ParseSetOperations();
        return ctes.Count == 0 ? body : AttachCtes(body, ctes, start, core.LastEnd);
    }

    private static Statement AttachCtes(Statement body, List<CommonTableExpr> ctes, int start, int end) => body switch
    {
        SelectStatement s => s with { Ctes = ctes, Start = start, Length = Math.Max(end, s.End) - start },
        SetOperation o => o with { Left = AttachCtes(o.Left, ctes, start, end), Start = start, Length = Math.Max(end, o.End) - start },
        _ => body,
    };

    private CommonTableExpr ParseCte()
    {
        var nameToken = core.Peek();
        var start = nameToken.Start;
        var name = string.Empty;
        if (nameToken.Kind is TokenKind.Identifier or TokenKind.QuotedIdentifier)
        {
            core.Next();
            name = core.NameOf(nameToken);
        }
        else
        {
            core.ReportMissing("Expected CTE name");
        }

        var columns = new List<string>();
        if (core.MatchPunct('('))
        {
            while (core.Peek().Kind is TokenKind.Identifier or TokenKind.QuotedIdentifier)
            {
                columns.Add(core.NameOf(core.Next()));
                if (!core.MatchPunct(','))
                {
                    break;
                }
            }

            core.Expect(')');
        }

        if (!core.Match("AS"))
        {
            core.ReportMissing("Expected AS");
        }

        core.Match("NOT");
        core.Match("MATERIALIZED");
        Statement query;
        if (core.MatchPunct('('))
        {
            query = ParseParenthesizedQuery();
        }
        else
        {
            core.ReportMissing("Expected '('");
            query = new UnknownStatement(core.LastEnd, 0);
        }

        return new CommonTableExpr(name, columns, query, start, core.LastEnd - start);
    }

    private Statement ParseSetOperations()
    {
        var left = ParseQueryTerm();
        while (true)
        {
            var op = core.Peek();
            SetOperator kind;
            if (core.IsWord(op, "UNION"))
            {
                kind = SetOperator.Union;
            }
            else if (core.IsWord(op, "INTERSECT"))
            {
                kind = SetOperator.Intersect;
            }
            else if (core.IsWord(op, "EXCEPT"))
            {
                kind = SetOperator.Except;
            }
            else
            {
                return left;
            }

            core.Next();
            var all = core.Match("ALL");
            if (!all)
            {
                core.Match("DISTINCT");
            }

            var right = ParseQueryTerm();
            left = new SetOperation(kind, all, left, right, left.Start, Math.Max(core.LastEnd, right.End) - left.Start);
        }
    }

    private Statement ParseQueryTerm()
    {
        if (core.MatchPunct('('))
        {
            return ParseParenthesizedQuery();
        }

        if (core.IsWord(core.Peek(), "SELECT"))
        {
            return ParseSelect();
        }

        core.ReportMissing("Expected SELECT");
        return new UnknownStatement(core.LastEnd, 0);
    }

    private SelectStatement ParseSelect()
    {
        var start = core.Next().Start;
        var distinct = false;
        if (core.Match("DISTINCT"))
        {
            distinct = true;
            if (core.Match("ON") && core.MatchPunct('('))
            {
                core.SkipToCloseParen();
                core.Expect(')');
            }
        }
        else
        {
            core.Match("ALL");
        }

        Expression? top = null;
        if (core.Dialect == DatabaseDialect.SqlServer && core.Match("TOP"))
        {
            top = _expressions.ParseOperand();
            core.Match("PERCENT");
            if (core.Match("WITH"))
            {
                core.Match("TIES");
            }
        }

        var items = ParseItems();
        var from = core.Match("FROM") ? ParseFrom() : null;
        var where = core.Match("WHERE") ? _expressions.ParseExpression() : null;
        var groupBy = new List<Expression>();
        if (core.Match("GROUP"))
        {
            ExpectBy();
            groupBy.Add(_expressions.ParseExpression());
            while (core.MatchPunct(','))
            {
                groupBy.Add(_expressions.ParseExpression());
            }
        }

        var having = core.Match("HAVING") ? _expressions.ParseExpression() : null;
        var orderBy = new List<OrderItem>();
        if (core.Match("ORDER"))
        {
            ExpectBy();
            ParseOrderItems(orderBy);
        }

        Expression? limit = null, offset = null;
        if (core.Match("LIMIT"))
        {
            limit = _expressions.ParseExpression();
            if (core.Match("OFFSET"))
            {
                offset = _expressions.ParseExpression();
            }
            else if (core.MatchPunct(','))
            {
                // MySQL: LIMIT offset, count
                offset = limit;
                limit = _expressions.ParseExpression();
            }
        }

        if (core.Match("OFFSET"))
        {
            offset = _expressions.ParseExpression();
            if (!core.Match("ROWS"))
            {
                core.Match("ROW");
            }
        }

        if (core.Match("FETCH"))
        {
            // shortcut: the FETCH row count is dropped, only OFFSET is kept; upgrade if a feature needs the count
            if (!core.Match("FIRST"))
            {
                core.Match("NEXT");
            }

            _expressions.ParseOperand();
            if (!core.Match("ROWS"))
            {
                core.Match("ROW");
            }

            core.Match("ONLY");
        }

        return new SelectStatement([], distinct, items, from, where, groupBy, having, orderBy, limit, offset, top,
            start, core.LastEnd - start);
    }

    private void ExpectBy()
    {
        if (!core.Match("BY"))
        {
            core.ReportMissing("Expected BY");
        }
    }

    private void ParseOrderItems(List<OrderItem> list)
    {
        do
        {
            var value = _expressions.ParseExpression();
            var descending = core.Match("DESC");
            if (!descending)
            {
                core.Match("ASC");
            }

            if (core.Match("NULLS") && !core.Match("FIRST"))
            {
                core.Match("LAST");
            }

            list.Add(new OrderItem(value, descending, value.Start, Math.Max(core.LastEnd, value.End) - value.Start));
        }
        while (core.MatchPunct(','));
    }

    private bool AtItemEnd()
    {
        var token = core.Peek();
        return token.Kind == TokenKind.EndOfFile || core.IsPunct(token, ')') || core.IsPunct(token, ';')
            || (token.Kind == TokenKind.Keyword && ExpressionParser.Boundary.Contains(core.TextOf(token))
                && !core.IsPunct(core.Peek(1), '('));
    }

    private List<SelectItem> ParseItems()
    {
        var items = new List<SelectItem>();
        while (true)
        {
            if (AtItemEnd())
            {
                core.ReportMissing("Expected select item");
                return items;
            }

            var value = _expressions.ParseExpression();
            var alias = ParseAlias(allowStringAfterAs: true);
            items.Add(new SelectItem(value, alias, value.Start, Math.Max(core.LastEnd, value.End) - value.Start));
            if (!core.MatchPunct(','))
            {
                return items;
            }
        }
    }

    /// <summary>Reads <c>[AS] alias</c>; a bare alias must be an identifier so clause keywords are never taken.</summary>
    private string? ParseAlias(bool allowStringAfterAs)
    {
        var token = core.Peek();
        if (core.Match("AS"))
        {
            token = core.Peek();
            var ok = token.Kind is TokenKind.Identifier or TokenKind.QuotedIdentifier
                || (allowStringAfterAs && token.Kind == TokenKind.StringLiteral)
                || (token.Kind == TokenKind.Keyword && !ExpressionParser.Boundary.Contains(core.TextOf(token)));
            if (!ok)
            {
                core.ReportMissing("Expected alias");
                return null;
            }
        }
        else if (token.Kind is not (TokenKind.Identifier or TokenKind.QuotedIdentifier))
        {
            return null;
        }

        core.Next();
        var text = core.NameOf(token);
        return token.Kind == TokenKind.StringLiteral && text.Length >= 2 ? text[1..^1] : text;
    }

    private TableSource ParseFrom()
    {
        var left = ParsePrimarySource();
        while (true)
        {
            var kind = PeekJoin(out var tokens);
            if (kind is null)
            {
                return left;
            }

            for (var i = 0; i < tokens; i++)
            {
                core.Next();
            }

            var right = ParsePrimarySource();
            Expression? on = null;
            if (core.Match("ON"))
            {
                on = _expressions.ParseExpression();
            }
            else if (core.Match("USING") && core.MatchPunct('('))
            {
                core.SkipToCloseParen();
                core.Expect(')');
            }

            left = new JoinSource(left, kind.Value, right, on, left.Start, Math.Max(core.LastEnd, right.End) - left.Start);
        }
    }

    /// <summary>Recognizes the tokens that introduce the next join, up to and including <c>JOIN</c> or the comma.</summary>
    private JoinKind? PeekJoin(out int tokens)
    {
        tokens = 1;
        if (core.IsPunct(core.Peek(), ','))
        {
            return JoinKind.Comma;
        }

        var count = 0;
        if (core.IsWord(core.Peek(count), "NATURAL"))
        {
            count++;
        }

        var kind = JoinKind.Inner;
        var word = core.Peek(count);
        foreach (var (name, joinKind) in (ReadOnlySpan<(string, JoinKind)>)
                 [("INNER", JoinKind.Inner), ("LEFT", JoinKind.Left), ("RIGHT", JoinKind.Right), ("FULL", JoinKind.Full), ("CROSS", JoinKind.Cross)])
        {
            if (core.IsWord(word, name))
            {
                kind = joinKind;
                count++;
                if (core.IsWord(core.Peek(count), "OUTER"))
                {
                    count++;
                }

                break;
            }
        }

        if (!core.IsWord(core.Peek(count), "JOIN"))
        {
            return null;
        }

        tokens = count + 1;
        return kind;
    }

    private TableSource ParsePrimarySource()
    {
        var first = core.Peek();
        if (core.MatchPunct('('))
        {
            var next = core.Peek();
            if (core.IsWord(next, "SELECT") || core.IsWord(next, "WITH") || core.IsPunct(next, '('))
            {
                var query = ParseParenthesizedQuery();
                var subAlias = ParseAlias(allowStringAfterAs: false);
                return new SubquerySource(query, subAlias, first.Start, core.LastEnd - first.Start);
            }

            var inner = ParseFrom();
            core.Expect(')');
            return inner;
        }

        var parts = new List<string>();
        while (true)
        {
            var token = core.Peek();
            if (token.Kind is TokenKind.Identifier or TokenKind.QuotedIdentifier
                || (token.Kind == TokenKind.Keyword && !ExpressionParser.Boundary.Contains(core.TextOf(token))))
            {
                core.Next();
                parts.Add(core.NameOf(token));
            }
            else
            {
                core.ReportMissing(parts.Count == 0 ? "Expected table name" : "Expected name after '.'");
                parts.Add(string.Empty);
                break;
            }

            if (!core.MatchPunct('.'))
            {
                break;
            }
        }

        if (parts[^1].Length > 0 && core.IsPunct(core.Peek(), '('))
        {
            // table-valued function call: arguments are not analyzed
            core.Next();
            core.SkipToCloseParen();
            core.Expect(')');
        }

        var alias = parts[^1].Length > 0 ? ParseAlias(allowStringAfterAs: false) : null;
        var start = parts.Count == 1 && parts[0].Length == 0 ? core.LastEnd : first.Start;
        return new TableRef(parts.Count > 1 ? parts[^2] : null, parts[^1], alias, start, core.LastEnd - start);
    }
}
