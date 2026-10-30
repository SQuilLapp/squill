using Squill.Sql.Lexing;

namespace Squill.Sql.Syntax;

/// <summary>Picks the parser for one statement by its first word; never throws and always returns a node.</summary>
/// <param name="core">The token cursor holding exactly one statement.</param>
internal sealed class StatementParser(SqlParserCore core)
{
    private static readonly string[] ColumnStops =
    [
        "NOT", "NULL", "DEFAULT", "PRIMARY", "REFERENCES", "UNIQUE", "CHECK", "CONSTRAINT", "COLLATE", "GENERATED",
        "AUTO_INCREMENT", "AUTOINCREMENT", "IDENTITY", "COMMENT", "AS",
    ];

    private static readonly string[] TableConstraints =
        ["CONSTRAINT", "PRIMARY", "FOREIGN", "UNIQUE", "CHECK", "KEY", "INDEX", "FULLTEXT", "EXCLUDE", "LIKE"];

    private static readonly string[] CreateModifiers =
        ["OR", "REPLACE", "TEMP", "TEMPORARY", "GLOBAL", "LOCAL", "UNLOGGED", "VOLATILE"];

    private readonly SelectParser _select = new(core);
    private readonly ExpressionParser _expressions = new(core);

    /// <summary>Parses the whole cursor as one statement.</summary>
    /// <returns>The statement node; an <see cref="UnknownStatement"/> for statements that are not modeled.</returns>
    public Statement Parse()
    {
        var first = core.Peek();
        if (core.IsWord(first, "INSERT"))
        {
            return Finish(first, ParseInsert);
        }

        if (core.IsWord(first, "UPDATE"))
        {
            return Finish(first, ParseUpdate);
        }

        if (core.IsWord(first, "DELETE"))
        {
            return Finish(first, ParseDelete);
        }

        if (core.IsWord(first, "CREATE") && IsCreateTable())
        {
            return Finish(first, ParseCreateTable);
        }

        if (core.IsWord(first, "SELECT") || core.IsWord(first, "WITH") || core.IsPunct(first, '('))
        {
            return _select.ParseStatement();
        }

        SkipRest();
        var word = first.Kind is TokenKind.Keyword or TokenKind.Identifier ? core.TextOf(first) : null;
        return new UnknownStatement(word, first.Start, Math.Max(0, core.LastEnd - first.Start));
    }

    private Statement Finish(Token first, Func<int, Statement> parse)
    {
        var node = parse(first.Start);
        SkipRest();
        return node with { Length = Math.Max(core.LastEnd, node.End) - node.Start };
    }

    private void SkipRest()
    {
        while (!core.AtEnd)
        {
            core.Next();
        }
    }

    private bool IsWordIn(Token token, string[] words) => words.Any(w => core.IsWord(token, w));

    /// <summary>Skips tokens up to, not including, a depth-0 word of <paramref name="stops"/> or the end.</summary>
    private void SkipUntil(params string[] stops)
    {
        var depth = 0;
        while (!core.AtEnd)
        {
            var token = core.Peek();
            if (depth == 0 && IsWordIn(token, stops))
            {
                return;
            }

            if (core.IsPunct(token, '('))
            {
                depth++;
            }
            else if (core.IsPunct(token, ')') && depth > 0)
            {
                depth--;
            }

            core.Next();
        }
    }

    private Statement ParseInsert(int start)
    {
        core.Next();
        core.Match("INTO");
        var target = _select.ParseNamedTable(allowCall: false, allowAlias: false);
        var columns = new List<string>();
        if (core.MatchPunct('('))
        {
            while (core.Peek().Kind is TokenKind.Identifier or TokenKind.QuotedIdentifier or TokenKind.Keyword)
            {
                columns.Add(core.NameOf(core.Next()));
                if (!core.MatchPunct(','))
                {
                    break;
                }
            }

            if (!core.IsPunct(core.Peek(), ')') && !core.AtEnd)
            {
                var extra = core.Peek();
                core.Report(extra.Start, extra.Length, $"Unexpected '{core.TextOf(extra)}'");
                core.SkipToCloseParen();
            }

            core.Expect(')');
        }

        // T-SQL OUTPUT sits between the column list and the source
        SkipUntil("VALUES", "VALUE", "SELECT", "WITH", "DEFAULT");
        Statement? source = null;
        var next = core.Peek();
        if (core.IsWord(next, "VALUES") || core.IsWord(next, "VALUE"))
        {
            source = ParseValues();
        }
        else if (core.IsWord(next, "SELECT") || core.IsWord(next, "WITH"))
        {
            source = _select.ParseQuery();
        }
        else if (!core.IsWord(next, "DEFAULT"))
        {
            core.ReportMissing("Expected VALUES or a query");
        }

        return new InsertStatement(target, columns, source, start, core.LastEnd - start);
    }

    private ValuesStatement ParseValues()
    {
        var start = core.Next().Start;
        var rows = new List<IReadOnlyList<Expression>>();
        do
        {
            var row = new List<Expression>();
            if (!core.Expect('('))
            {
                break;
            }

            while (!core.IsPunct(core.Peek(), ')') && !core.AtEnd)
            {
                row.Add(_expressions.ParseExpression());
                if (!core.MatchPunct(','))
                {
                    break;
                }
            }

            if (!core.IsPunct(core.Peek(), ')') && !core.AtEnd)
            {
                var extra = core.Peek();
                core.Report(extra.Start, extra.Length, $"Unexpected '{core.TextOf(extra)}'");
                core.SkipToCloseParen();
            }

            core.Expect(')');
            rows.Add(row);
        }
        while (core.MatchPunct(','));

        return new ValuesStatement(rows, start, core.LastEnd - start);
    }

    private Statement ParseUpdate(int start)
    {
        core.Next();
        var target = _select.ParseNamedTable(allowCall: false, allowAlias: true);
        var set = new List<Assignment>();
        if (core.Match("SET"))
        {
            ParseAssignments(set);
        }
        else
        {
            core.ReportMissing("Expected SET");
        }

        SkipUntil("FROM", "WHERE");
        var from = core.Match("FROM") ? _select.ParseFrom() : null;
        var where = core.Match("WHERE") ? _expressions.ParseExpression() : null;
        return new UpdateStatement(target, set, from, where, start, core.LastEnd - start);
    }

    private void ParseAssignments(List<Assignment> set)
    {
        do
        {
            var token = core.Peek();
            if (!IsName(token) || IsClauseWord(token))
            {
                core.ReportMissing("Expected column name");
                return;
            }

            var column = core.NameOf(core.Next());
            while (core.MatchPunct('.'))
            {
                if (IsName(core.Peek()))
                {
                    column = core.NameOf(core.Next());
                }
                else
                {
                    core.ReportMissing("Expected name after '.'");
                    column = string.Empty;
                }
            }

            if (!core.IsOperator(core.Peek(), "="))
            {
                core.ReportMissing("Expected '='");
            }
            else
            {
                core.Next();
            }

            var value = _expressions.ParseExpression();
            set.Add(new Assignment(column, value, token.Start, Math.Max(core.LastEnd, value.End) - token.Start));
        }
        while (core.MatchPunct(','));
    }

    private static bool IsName(Token token) => token.Kind is TokenKind.Identifier or TokenKind.QuotedIdentifier or TokenKind.Keyword;

    private bool IsClauseWord(Token token) =>
        IsWordIn(token, ["FROM", "WHERE", "OUTPUT", "RETURNING"]);

    private Statement ParseDelete(int start)
    {
        core.Next();
        core.Match("FROM");
        var target = _select.ParseNamedTable(allowCall: false, allowAlias: true);
        TableSource? from = null;
        SkipUntil("FROM", "WHERE");
        while (core.Match("FROM"))
        {
            from = _select.ParseFrom();
            SkipUntil("FROM", "WHERE");
        }

        var where = core.Match("WHERE") ? _expressions.ParseExpression() : null;
        return new DeleteStatement(target, from, where, start, core.LastEnd - start);
    }

    private bool IsCreateTable()
    {
        for (var i = 1; ; i++)
        {
            var token = core.Peek(i);
            if (core.IsWord(token, "TABLE"))
            {
                return true;
            }

            if (!IsWordIn(token, CreateModifiers))
            {
                return false;
            }
        }
    }

    private Statement ParseCreateTable(int start)
    {
        while (!core.Match("TABLE"))
        {
            core.Next();
        }

        if (core.Match("IF"))
        {
            core.Match("NOT");
            core.Match("EXISTS");
        }

        var name = _select.ParseNamedTable(allowCall: false, allowAlias: false);
        var columns = new List<ColumnDef>();
        if (core.MatchPunct('('))
        {
            do
            {
                if (core.IsPunct(core.Peek(), ')') || core.AtEnd)
                {
                    break;
                }

                var column = ParseColumnDef();
                if (column is not null)
                {
                    columns.Add(column);
                }

                SkipEntry();
            }
            while (core.MatchPunct(','));

            core.Expect(')');
        }

        return new CreateTableStatement(name, columns, start, core.LastEnd - start);
    }

    private ColumnDef? ParseColumnDef()
    {
        var token = core.Peek();
        if (!IsName(token) || (token.Kind == TokenKind.Keyword && IsWordIn(token, TableConstraints)))
        {
            return null;
        }

        core.Next();
        var typeStart = core.Peek().Start;
        var typeEnd = typeStart;
        var depth = 0;
        while (!core.AtEnd)
        {
            var t = core.Peek();
            if (depth == 0 && (core.IsPunct(t, ',') || core.IsPunct(t, ')') || IsWordIn(t, ColumnStops)))
            {
                break;
            }

            if (core.IsPunct(t, '('))
            {
                depth++;
            }
            else if (core.IsPunct(t, ')'))
            {
                depth--;
            }

            core.Next();
            typeEnd = t.End;
        }

        var typeName = typeEnd > typeStart ? core.Text[typeStart..typeEnd] : string.Empty;
        return new ColumnDef(core.NameOf(token), typeName, token.Start, Math.Max(core.LastEnd, token.End) - token.Start);
    }

    /// <summary>Skips the rest of a column or constraint entry, up to its depth-0 comma or the closing parenthesis.</summary>
    private void SkipEntry()
    {
        var depth = 0;
        while (!core.AtEnd)
        {
            var token = core.Peek();
            if (depth == 0 && (core.IsPunct(token, ',') || core.IsPunct(token, ')')))
            {
                return;
            }

            if (core.IsPunct(token, '('))
            {
                depth++;
            }
            else if (core.IsPunct(token, ')'))
            {
                depth--;
            }

            core.Next();
        }
    }
}
