using System.Globalization;
using System.Text;
using Squill.Core;
using Squill.Core.Plans;

namespace Squill.Data.Oracle;

/// <summary>One row of <c>PLAN_TABLE</c> after <c>EXPLAIN PLAN</c>.</summary>
/// <param name="Id">Step id, 0 for the statement itself.</param>
/// <param name="ParentId">Id of the parent step, <see langword="null"/> for the root.</param>
/// <param name="Operation">Operation, for example <c>TABLE ACCESS</c>.</param>
/// <param name="Options">Variant of the operation, for example <c>FULL</c>.</param>
/// <param name="Owner">Schema of the object the step reads.</param>
/// <param name="Name">Table or index the step reads.</param>
/// <param name="Cardinality">Estimated rows.</param>
/// <param name="Cost">Cumulative estimated cost.</param>
/// <param name="Bytes">Estimated bytes.</param>
/// <param name="Access">Access predicates.</param>
/// <param name="Filter">Filter predicates.</param>
internal sealed record OraclePlanRow(
    long Id, long? ParentId, string Operation, string? Options, string? Owner, string? Name, double? Cardinality, double? Cost, double? Bytes, string? Access, string? Filter);

/// <summary>Turns the rows of <c>PLAN_TABLE</c> into an engine-neutral <see cref="QueryPlan"/>; pure, never throws on unknown operations.</summary>
internal static class OraclePlanParser
{
    /// <summary>Builds the plan tree from the rows.</summary>
    /// <param name="statement">The explained statement.</param>
    /// <param name="rows">The rows of one statement id.</param>
    /// <param name="text">The <c>DBMS_XPLAN</c> text, or <see langword="null"/> to draw an indented tree from the rows.</param>
    /// <returns>The plan; a synthetic root when the rows have none.</returns>
    public static QueryPlan Parse(string statement, IReadOnlyList<OraclePlanRow> rows, string? text)
    {
        var byParent = rows.Where(r => r.ParentId is not null).ToLookup(r => r.ParentId!.Value);
        var ids = rows.Select(r => r.Id).ToHashSet();
        var visited = new HashSet<long>();
        var tops = rows.Where(r => r.ParentId is null || !ids.Contains(r.ParentId.Value)).Select(r => Build(r, byParent, visited)).ToList();
        var root = tops.Count == 1 ? tops[0] : Node("PLAN", PlanNodeKind.Result, null, null, null, null, null, [], tops);
        return new QueryPlan(
            DatabaseDialect.Oracle, PlanMode.Estimated, statement, PlanIds.Assign(root), [], [],
            null, null, null, false, PlanSourceFormat.Text, text ?? Draw(root));
    }

    private static PlanNode Build(OraclePlanRow row, ILookup<long, OraclePlanRow> byParent, HashSet<long> visited)
    {
        var children = visited.Add(row.Id)
            ? byParent[row.Id].Where(c => c.Id != row.Id).Select(c => Build(c, byParent, visited)).ToList()
            : [];
        var operation = row.Options is { Length: > 0 } o ? $"{row.Operation} {o}" : row.Operation;
        var target = row.Name is null ? null : row.Owner is { Length: > 0 } owner ? $"{owner}.{row.Name}" : row.Name;
        var detail = string.Join(" · ", new[] { row.Access is null ? null : "access: " + row.Access, row.Filter is null ? null : "filter: " + row.Filter }.Where(d => d is not null));
        PlanWarning[] warnings = Kind(row) == PlanNodeKind.TableScan && target is not null ? [new PlanWarning(PlanMessageCodes.NoIndex, [target])] : [];
        return Node(operation, Kind(row), target, detail.Length == 0 ? null : detail, row.Cardinality, row.Cost, row.Bytes, warnings, children);
    }

    private static PlanNodeKind Kind(OraclePlanRow row)
    {
        var options = row.Options ?? "";
        return row.Operation switch
        {
            "SELECT STATEMENT" or "INSERT STATEMENT" or "UPDATE STATEMENT" or "DELETE STATEMENT" or "MERGE STATEMENT" => PlanNodeKind.Result,
            "TABLE ACCESS" when options.StartsWith("FULL", StringComparison.Ordinal) => PlanNodeKind.TableScan,
            "TABLE ACCESS" => PlanNodeKind.IndexSeek,
            "INDEX" when options.Contains("RANGE", StringComparison.Ordinal) || options.Contains("UNIQUE", StringComparison.Ordinal) => PlanNodeKind.IndexSeek,
            "INDEX" => PlanNodeKind.IndexScan,
            "SORT" when options.StartsWith("ORDER BY", StringComparison.Ordinal) || options.StartsWith("JOIN", StringComparison.Ordinal) => PlanNodeKind.Sort,
            "SORT" or "HASH" => PlanNodeKind.Aggregate,
            "COUNT" when options.Contains("STOPKEY", StringComparison.Ordinal) => PlanNodeKind.Limit,
            "FILTER" => PlanNodeKind.Filter,
            "VIEW" => PlanNodeKind.Subquery,
            "UNION-ALL" or "UNION" or "MINUS" or "INTERSECTION" => PlanNodeKind.Union,
            "BUFFER" or "TEMP TABLE TRANSFORMATION" => PlanNodeKind.Materialize,
            var o when o.StartsWith("PX", StringComparison.Ordinal) => PlanNodeKind.Parallel,
            var o when o.Contains("JOIN", StringComparison.Ordinal) || o is "NESTED LOOPS" => PlanNodeKind.Join,
            _ => PlanNodeKind.Other,
        };
    }

    private static PlanNode Node(
        string operation, PlanNodeKind kind, string? target, string? detail, double? rows, double? cost, double? bytes, IReadOnlyList<PlanWarning> warnings, IReadOnlyList<PlanNode> children) =>
        new(0, operation, kind, target, detail, rows, cost, null, bytes is null ? null : (int)Math.Min(int.MaxValue, bytes.Value / Math.Max(1, rows ?? 1)), null, null, null, [], warnings, false, children);

    private static string Draw(PlanNode root)
    {
        var text = new StringBuilder();
        Append(text, root, 0);
        return text.ToString().TrimEnd();
    }

    private static void Append(StringBuilder text, PlanNode node, int depth)
    {
        text.Append(' ', depth * 2).Append(node.Operation);
        if (node.Target is not null)
            text.Append(' ').Append(node.Target);
        if (node.EstimatedCost is { } cost)
            text.Append(CultureInfo.InvariantCulture, $"  (cost {cost:0.##})");
        text.Append('\n');
        foreach (var child in node.Children)
            Append(text, child, depth + 1);
    }
}
