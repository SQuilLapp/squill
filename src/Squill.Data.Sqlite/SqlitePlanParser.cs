using System.Text;
using Squill.Core;
using Squill.Core.Plans;

namespace Squill.Data.Sqlite;

/// <summary>One row of <c>EXPLAIN QUERY PLAN</c>.</summary>
/// <param name="Id">Row id.</param>
/// <param name="Parent">Id of the parent row, 0 for a top-level row.</param>
/// <param name="Detail">Human-readable description of the step.</param>
internal sealed record SqlitePlanRow(long Id, long Parent, string Detail);

/// <summary>Turns the rows of <c>EXPLAIN QUERY PLAN</c> into an engine-neutral <see cref="QueryPlan"/>; pure, never throws on unknown text.</summary>
internal static class SqlitePlanParser
{
    private static readonly string[] CompoundPrefixes = ["COMPOUND QUERY", "LEFT-MOST SUBQUERY", "MERGE (", "UNION", "EXCEPT", "INTERSECT"];
    private static readonly string[] SubqueryPrefixes = ["CORRELATED SCALAR SUBQUERY", "CORRELATED LIST SUBQUERY", "SCALAR SUBQUERY", "LIST SUBQUERY", "CO-ROUTINE"];

    /// <summary>Builds the plan tree from the rows.</summary>
    /// <param name="statement">The explained statement.</param>
    /// <param name="rows">Rows in the order SQLite returned them.</param>
    /// <returns>The plan: a single root, a synthetic <c>Query</c> node when several rows are top-level.</returns>
    public static QueryPlan Parse(string statement, IReadOnlyList<SqlitePlanRow> rows)
    {
        var byParent = rows.ToLookup(r => r.Parent);
        var ids = rows.Select(r => r.Id).ToHashSet();
        // A row whose parent is unknown is treated as top-level, so malformed input still gives a tree.
        var top = rows.Where(r => r.Parent == 0 || !ids.Contains(r.Parent)).ToList();
        var visited = new HashSet<long>();
        var tops = top.Select(r => Build(r, byParent, visited)).ToList();
        var root = tops.Count == 1
            ? tops[0]
            : Node("Query", PlanNodeKind.Result, null, null, [], tops);
        return new QueryPlan(
            DatabaseDialect.Sqlite, PlanMode.Estimated, statement, PlanIds.Assign(root), [], [],
            null, null, null, false, PlanSourceFormat.Text, ToText(rows, byParent, top));
    }

    private static PlanNode Build(SqlitePlanRow row, ILookup<long, SqlitePlanRow> byParent, HashSet<long> visited)
    {
        var children = new List<PlanNode>();
        if (visited.Add(row.Id))
            children.AddRange(byParent[row.Id].Where(c => c.Id != row.Id).Select(c => Build(c, byParent, visited)));
        return Map(row.Detail.Trim(), children);
    }

    private static PlanNode Map(string detail, List<PlanNode> children)
    {
        if (StartsWith(detail, "SCAN "))
        {
            var rest = detail[5..];
            if (StartsWith(rest, "TABLE "))
                rest = rest[6..];
            if (StartsWith(rest, "SUBQUERY"))
                return Node("SCAN", PlanNodeKind.Subquery, rest, null, [], children);
            var usingAt = rest.IndexOf(" USING ", StringComparison.OrdinalIgnoreCase);
            var target = usingAt < 0 ? rest : rest[..usingAt];
            var index = usingAt < 0 ? null : rest[(usingAt + 7)..];
            var hasIndex = index is not null && index.Contains("INDEX", StringComparison.OrdinalIgnoreCase);
            var constant = target.Equals("CONSTANT ROW", StringComparison.OrdinalIgnoreCase);
            PlanWarning[] warnings = !hasIndex && !constant && !target.Contains("VIRTUAL TABLE", StringComparison.OrdinalIgnoreCase)
                ? [new PlanWarning(PlanMessageCodes.NoIndex, [target])]
                : [];
            return Node("SCAN", hasIndex ? PlanNodeKind.IndexScan : PlanNodeKind.TableScan, target, index, warnings, children);
        }

        if (StartsWith(detail, "SEARCH "))
        {
            var rest = detail[7..];
            if (StartsWith(rest, "TABLE "))
                rest = rest[6..];
            var usingAt = rest.IndexOf(" USING ", StringComparison.OrdinalIgnoreCase);
            if (usingAt < 0)
                return Node("SEARCH", PlanNodeKind.IndexSeek, rest, null, [], children);
            var table = rest[..usingAt];
            var how = rest[(usingAt + 7)..];
            string? condition = null;
            var leftJoin = how.EndsWith(" LEFT-JOIN", StringComparison.Ordinal);
            if (leftJoin)
                how = how[..^10];
            var open = how.IndexOf(" (", StringComparison.Ordinal);
            if (open >= 0 && how.EndsWith(')'))
            {
                condition = how[(open + 2)..^1];
                how = how[..open];
            }
            foreach (var prefix in new[] { "COVERING INDEX ", "INDEX " })
            {
                if (StartsWith(how, prefix))
                {
                    how = how[prefix.Length..];
                    break;
                }
            }
            return Node("SEARCH", PlanNodeKind.IndexSeek, table + " · " + how, leftJoin ? (condition + " LEFT-JOIN").TrimStart() : condition, [], children);
        }

        if (StartsWith(detail, "USE TEMP B-TREE"))
        {
            var forWhat = detail[15..].Trim();
            var kind = forWhat.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase) ? PlanNodeKind.Sort
                : forWhat.Contains("GROUP BY", StringComparison.OrdinalIgnoreCase) || forWhat.Contains("DISTINCT", StringComparison.OrdinalIgnoreCase) ? PlanNodeKind.Aggregate
                : PlanNodeKind.Other;
            return Node("USE TEMP B-TREE", kind, null, forWhat.Length == 0 ? null : forWhat, [], children);
        }

        foreach (var prefix in CompoundPrefixes)
        {
            if (StartsWith(detail, prefix))
                return Node(detail, PlanNodeKind.Union, null, null, [], children);
        }

        foreach (var prefix in SubqueryPrefixes)
        {
            if (StartsWith(detail, prefix))
                return Node(prefix, PlanNodeKind.Subquery, null, Rest(detail, prefix), [], children);
        }

        return StartsWith(detail, "MATERIALIZE")
            ? Node("MATERIALIZE", PlanNodeKind.Materialize, null, Rest(detail, "MATERIALIZE"), [], children)
            : Node(detail, PlanNodeKind.Other, null, null, [], children);
    }

    private static string? Rest(string detail, string prefix) => detail.Length > prefix.Length ? detail[prefix.Length..].Trim() : null;

    private static bool StartsWith(string text, string prefix) => text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    private static PlanNode Node(string operation, PlanNodeKind kind, string? target, string? detail, IReadOnlyList<PlanWarning> warnings, IReadOnlyList<PlanNode> children) =>
        new(0, operation, kind, target, detail, null, null, null, null, null, null, null, [], warnings, false, children);

    /// <summary>Renders the rows like the <c>sqlite3</c> shell: <c>QUERY PLAN</c>, then <c>|--</c> and <c>`--</c> branches.</summary>
    private static string ToText(IReadOnlyList<SqlitePlanRow> rows, ILookup<long, SqlitePlanRow> byParent, List<SqlitePlanRow> top)
    {
        var text = new StringBuilder("QUERY PLAN");
        var seen = new HashSet<long>();
        Append(text, top, byParent, "", seen);
        return text.ToString();
    }

    private static void Append(StringBuilder text, List<SqlitePlanRow> level, ILookup<long, SqlitePlanRow> byParent, string indent, HashSet<long> seen)
    {
        for (var i = 0; i < level.Count; i++)
        {
            var last = i == level.Count - 1;
            text.Append('\n').Append(indent).Append(last ? "`--" : "|--").Append(level[i].Detail);
            if (seen.Add(level[i].Id))
                Append(text, byParent[level[i].Id].Where(c => c.Id != level[i].Id).ToList(), byParent, indent + (last ? "   " : "|  "), seen);
        }
    }
}
