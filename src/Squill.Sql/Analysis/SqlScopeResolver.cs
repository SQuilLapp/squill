using Squill.Sql.Lexing;
using Squill.Sql.Syntax;

namespace Squill.Sql.Analysis;

/// <summary>Finds the clause and the visible tables, aliases and CTEs at a caret position.</summary>
public static class SqlScopeResolver
{
    /// <summary>Analyzes the statement around <paramref name="offset"/>; never throws.</summary>
    /// <param name="script">A script parsed by <see cref="SqlParser.Parse"/>.</param>
    /// <param name="offset">The caret offset in <see cref="SqlScript.Text"/>.</param>
    /// <returns>The clause and the sources, empty when the caret is outside every statement.</returns>
    public static ScopeInfo Resolve(SqlScript script, int offset)
    {
        var text = script.Text;
        offset = Math.Clamp(offset, 0, text.Length);
        var chain = new List<Frame>();
        foreach (var statement in script.Statements)
        {
            if (Contains(statement, offset, text, nested: false))
            {
                Descend(statement, [], text, offset, chain);
                break;
            }
        }

        if (chain.Count == 0)
        {
            return new ScopeInfo(ClauseKind.None, []);
        }

        var sources = new List<VisibleSource>();
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            AddSources(chain[i], sources);
        }

        return new ScopeInfo(ClauseOf(chain[^1].Statement, script, offset), sources);
    }

    private sealed record Frame(Statement Statement, IReadOnlyList<CommonTableExpr> Visible);

    /// <summary>Tests whether the caret is in a node, or just after an unfinished one (only blanks, then end or a closing parenthesis).</summary>
    private static bool Contains(SyntaxNode node, int offset, string text, bool nested)
    {
        if (offset < node.Start)
        {
            return false;
        }

        if (offset <= node.End)
        {
            return true;
        }

        if (!text.AsSpan(node.End, offset - node.End).IsWhiteSpace())
        {
            return false;
        }

        if (!nested)
        {
            return true;
        }

        var rest = text.AsSpan(offset).TrimStart();
        return rest.IsEmpty || rest[0] == ')';
    }

    private static IReadOnlyList<CommonTableExpr> Leading(Statement statement) => statement switch
    {
        SelectStatement s => s.Ctes,
        SetOperation o => Leading(o.Left),
        _ => [],
    };

    private static void Descend(
        Statement statement, IReadOnlyList<CommonTableExpr> inherited, string text, int offset, List<Frame> chain)
    {
        var visible = new List<CommonTableExpr>(inherited);
        visible.AddRange(Leading(statement));
        if (statement is SetOperation op)
        {
            if (Contains(op.Left, offset, text, nested: true))
            {
                Descend(op.Left, inherited, text, offset, chain);
            }
            else if (Contains(op.Right, offset, text, nested: true))
            {
                Descend(op.Right, visible, text, offset, chain);
            }

            return;
        }

        if (statement is SelectStatement or UpdateStatement or DeleteStatement or InsertStatement)
        {
            chain.Add(new Frame(statement, visible));
        }

        foreach (var (child, childInherited) in Children(statement, inherited, visible))
        {
            if (Contains(child, offset, text, nested: true))
            {
                Descend(child, childInherited, text, offset, chain);
                return;
            }
        }
    }

    private static IEnumerable<(Statement Query, IReadOnlyList<CommonTableExpr> Inherited)> Children(
        Statement statement, IReadOnlyList<CommonTableExpr> inherited, IReadOnlyList<CommonTableExpr> visible)
    {
        var queries = new List<Statement>();
        switch (statement)
        {
            case SelectStatement s:
                for (var i = 0; i < s.Ctes.Count; i++)
                {
                    yield return (s.Ctes[i].Query, [.. inherited, .. s.Ctes.Take(i)]);
                }

                Source(s.From, queries);
                foreach (var item in s.Items)
                {
                    Expr(item.Value, queries);
                }

                Expr(s.Where, queries);
                foreach (var e in s.GroupBy)
                {
                    Expr(e, queries);
                }

                Expr(s.Having, queries);
                foreach (var o in s.OrderBy)
                {
                    Expr(o.Value, queries);
                }

                break;
            case InsertStatement ins:
                if (ins.Source is not null)
                {
                    queries.Add(ins.Source);
                }

                break;
            case UpdateStatement u:
                foreach (var a in u.Set)
                {
                    Expr(a.Value, queries);
                }

                Source(u.From, queries);
                Expr(u.Where, queries);
                break;
            case DeleteStatement d:
                Source(d.From, queries);
                Expr(d.Where, queries);
                break;
            case ValuesStatement v:
                foreach (var e in v.Rows.SelectMany(r => r))
                {
                    Expr(e, queries);
                }

                break;
        }

        foreach (var query in queries)
        {
            yield return (query, visible);
        }
    }

    private static void Source(TableSource? source, List<Statement> queries)
    {
        switch (source)
        {
            case SubquerySource s:
                queries.Add(s.Query);
                break;
            case JoinSource j:
                Source(j.Left, queries);
                Source(j.Right, queries);
                Expr(j.On, queries);
                break;
        }
    }

    private static void Expr(Expression? expression, List<Statement> queries)
    {
        switch (expression)
        {
            case SubqueryExpr q:
                queries.Add(q.Query);
                break;
            case BinaryExpr b:
                Expr(b.Left, queries);
                Expr(b.Right, queries);
                break;
            case UnaryExpr u:
                Expr(u.Operand, queries);
                break;
            case FunctionCall f:
                foreach (var a in f.Args)
                {
                    Expr(a, queries);
                }

                break;
            case CaseExpr c:
                Expr(c.Operand, queries);
                foreach (var w in c.Whens)
                {
                    Expr(w.Condition, queries);
                    Expr(w.Result, queries);
                }

                Expr(c.Else, queries);
                break;
            case CastExpr c:
                Expr(c.Operand, queries);
                break;
            case ParenExpr p:
                Expr(p.Inner, queries);
                break;
            case ListExpr l:
                foreach (var e in l.Items)
                {
                    Expr(e, queries);
                }

                break;
        }
    }

    private static void AddSources(Frame frame, List<VisibleSource> sources)
    {
        switch (frame.Statement)
        {
            case SelectStatement s:
                Flatten(s.From, frame.Visible, sources);
                break;
            case InsertStatement i:
                Flatten(i.Target, frame.Visible, sources);
                break;
            case UpdateStatement u:
                Flatten(u.Target, frame.Visible, sources);
                Flatten(u.From, frame.Visible, sources);
                break;
            case DeleteStatement d:
                var before = sources.Count;
                Flatten(d.From, frame.Visible, sources);
                // T-SQL "DELETE t FROM t JOIN ...": the FROM already lists the target
                if (!sources.Skip(before).Any(x => string.Equals(x.Name, d.Target.Name, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.Alias, d.Target.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    Flatten(d.Target, frame.Visible, sources);
                }

                break;
        }
    }

    private static void Flatten(TableSource? source, IReadOnlyList<CommonTableExpr> ctes, List<VisibleSource> sources)
    {
        switch (source)
        {
            case TableRef { Name.Length: > 0 } t:
                var cte = t.Schema is null
                    ? ctes.LastOrDefault(c => string.Equals(c.Name, t.Name, StringComparison.OrdinalIgnoreCase))
                    : null;
                sources.Add(cte is null
                    ? new VisibleSource(t.Schema, t.Name, t.Alias, null)
                    : new VisibleSource(null, cte.Name, t.Alias, cte.Columns.Count > 0 ? cte.Columns : OutputNames(cte.Query)));
                break;
            case SubquerySource s:
                sources.Add(new VisibleSource(null, s.Alias ?? string.Empty, s.Alias, OutputNames(s.Query)));
                break;
            case JoinSource j:
                Flatten(j.Left, ctes, sources);
                Flatten(j.Right, ctes, sources);
                break;
        }
    }

    /// <summary>Gets the output names of a query, or <see langword="null"/> when a star or a non-select makes them unknown.</summary>
    private static IReadOnlyList<string>? OutputNames(Statement query)
    {
        switch (query)
        {
            case SetOperation o:
                return OutputNames(o.Left);
            case SelectStatement s:
                var names = new List<string>();
                foreach (var item in s.Items)
                {
                    if (item.Value is StarExpr)
                    {
                        return null;
                    }

                    var name = item.Alias ?? (item.Value as ColumnRef)?.Name;
                    if (!string.IsNullOrEmpty(name))
                    {
                        names.Add(name);
                    }
                }

                return names;
            default:
                return null;
        }
    }

    private static ClauseKind ClauseOf(Statement statement, SqlScript script, int offset)
    {
        var clause = ClauseKind.None;
        var depth = 0;
        foreach (var token in SqlTokenizers.For(script.Dialect).Tokenize(script.Text))
        {
            if (token.Start < statement.Start || token.End > offset)
            {
                continue;
            }

            if (token.Kind == TokenKind.Punctuation)
            {
                var c = script.Text[token.Start];
                depth = c == '(' ? depth + 1 : c == ')' ? Math.Max(0, depth - 1) : depth;
                continue;
            }

            if (depth > 0 || token.Kind is not (TokenKind.Keyword or TokenKind.Identifier))
            {
                continue;
            }

            clause = token.Text(script.Text).ToUpperInvariant() switch
            {
                "SELECT" => ClauseKind.Select,
                "FROM" => ClauseKind.From,
                "JOIN" => ClauseKind.Join,
                "ON" or "USING" when clause is ClauseKind.From or ClauseKind.Join => ClauseKind.On,
                "WHERE" => ClauseKind.Where,
                "GROUP" => ClauseKind.GroupBy,
                "HAVING" => ClauseKind.Having,
                "ORDER" => ClauseKind.OrderBy,
                "SET" => ClauseKind.Set,
                "VALUES" => ClauseKind.Values,
                "WITH" or "INSERT" or "UPDATE" or "DELETE" or "INTO" or "LIMIT" or "OFFSET" or "FETCH" or "RETURNING"
                    or "OUTPUT" => ClauseKind.Other,
                _ => clause,
            };
        }

        return clause;
    }
}
