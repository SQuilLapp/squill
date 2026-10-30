using Squill.Sql.Syntax;

namespace Squill.Sql.Analysis;

/// <summary>A table or column reference found in a syntax tree, with what was visible around it.</summary>
/// <param name="Node">A <see cref="TableRef"/> or a <see cref="ColumnRef"/>.</param>
/// <param name="Ctes">The names of the common table expressions visible at the node.</param>
/// <param name="Aliases">The select-list aliases of the enclosing query, which an unqualified column may name.</param>
internal sealed record TreeRef(SyntaxNode Node, IReadOnlyList<string> Ctes, IReadOnlySet<string> Aliases);

/// <summary>Collects every table and column reference of a script.</summary>
internal static class SqlTreeWalker
{
    private static readonly IReadOnlySet<string> NoAliases = new HashSet<string>();

    /// <summary>Lists the references of every statement of <paramref name="script"/>.</summary>
    /// <param name="script">A parsed script.</param>
    /// <returns>The table and column references, in tree order.</returns>
    public static List<TreeRef> References(SqlScript script)
    {
        var refs = new List<TreeRef>();
        foreach (var statement in script.Statements)
        {
            Walk(statement, [], refs);
        }

        return refs;
    }

    private static IReadOnlyList<string> Leading(Statement statement) => statement switch
    {
        SelectStatement s => [.. s.Ctes.Select(c => c.Name)],
        SetOperation o => Leading(o.Left),
        _ => [],
    };

    private static void Walk(Statement statement, IReadOnlyList<string> ctes, List<TreeRef> refs)
    {
        switch (statement)
        {
            case SelectStatement s:
                for (var i = 0; i < s.Ctes.Count; i++)
                {
                    Walk(s.Ctes[i].Query, [.. ctes, .. s.Ctes.Take(i + 1).Select(c => c.Name)], refs);
                }

                IReadOnlyList<string> visible = [.. ctes, .. s.Ctes.Select(c => c.Name)];
                IReadOnlySet<string> own = s.Items.Select(i => i.Alias).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
                Source(s.From, visible, own, refs);
                foreach (var item in s.Items)
                {
                    Expr(item.Value, visible, own, refs);
                }

                Expr(s.Where, visible, own, refs);
                foreach (var e in s.GroupBy)
                {
                    Expr(e, visible, own, refs);
                }

                Expr(s.Having, visible, own, refs);
                foreach (var o in s.OrderBy)
                {
                    Expr(o.Value, visible, own, refs);
                }

                break;
            case SetOperation op:
                Walk(op.Left, ctes, refs);
                Walk(op.Right, [.. ctes, .. Leading(op.Left)], refs);
                break;
            case InsertStatement ins:
                Source(ins.Target, ctes, NoAliases, refs);
                if (ins.Source is not null)
                {
                    Walk(ins.Source, ctes, refs);
                }

                break;
            case UpdateStatement u:
                Source(u.Target, ctes, NoAliases, refs);
                foreach (var a in u.Set)
                {
                    Expr(a.Value, ctes, NoAliases, refs);
                }

                Source(u.From, ctes, NoAliases, refs);
                Expr(u.Where, ctes, NoAliases, refs);
                break;
            case DeleteStatement d:
                // T-SQL "DELETE t FROM t ...": the target may be an alias of the FROM, which lists the real table
                Source(d.From ?? d.Target, ctes, NoAliases, refs);
                Expr(d.Where, ctes, NoAliases, refs);
                break;
            case ValuesStatement v:
                foreach (var e in v.Rows.SelectMany(r => r))
                {
                    Expr(e, ctes, NoAliases, refs);
                }

                break;
        }
    }

    private static void Source(TableSource? source, IReadOnlyList<string> ctes, IReadOnlySet<string> aliases, List<TreeRef> refs)
    {
        switch (source)
        {
            case TableRef { Name.Length: > 0 } t:
                refs.Add(new TreeRef(t, ctes, aliases));
                break;
            case SubquerySource s:
                Walk(s.Query, ctes, refs);
                break;
            case JoinSource j:
                Source(j.Left, ctes, aliases, refs);
                Source(j.Right, ctes, aliases, refs);
                Expr(j.On, ctes, aliases, refs);
                break;
        }
    }

    private static void Expr(Expression? expression, IReadOnlyList<string> ctes, IReadOnlySet<string> aliases, List<TreeRef> refs)
    {
        switch (expression)
        {
            case ColumnRef { Name.Length: > 0 } c:
                refs.Add(new TreeRef(c, ctes, aliases));
                break;
            case SubqueryExpr q:
                Walk(q.Query, ctes, refs);
                break;
            case BinaryExpr b:
                Expr(b.Left, ctes, aliases, refs);
                Expr(b.Right, ctes, aliases, refs);
                break;
            case UnaryExpr u:
                Expr(u.Operand, ctes, aliases, refs);
                break;
            case FunctionCall f:
                foreach (var a in f.Args)
                {
                    Expr(a, ctes, aliases, refs);
                }

                break;
            case CaseExpr c:
                Expr(c.Operand, ctes, aliases, refs);
                foreach (var w in c.Whens)
                {
                    Expr(w.Condition, ctes, aliases, refs);
                    Expr(w.Result, ctes, aliases, refs);
                }

                Expr(c.Else, ctes, aliases, refs);
                break;
            case CastExpr c:
                Expr(c.Operand, ctes, aliases, refs);
                break;
            case ParenExpr p:
                Expr(p.Inner, ctes, aliases, refs);
                break;
            case ListExpr l:
                foreach (var e in l.Items)
                {
                    Expr(e, ctes, aliases, refs);
                }

                break;
        }
    }
}
