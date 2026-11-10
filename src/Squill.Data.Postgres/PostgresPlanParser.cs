using System.Globalization;
using System.Text.Json;
using Squill.Core;
using Squill.Core.Plans;

namespace Squill.Data.Postgres;

/// <summary>Turns the <c>EXPLAIN (FORMAT JSON)</c> document of PostgreSQL into an engine-neutral <see cref="QueryPlan"/>; pure, never throws on unknown keys.</summary>
internal static class PostgresPlanParser
{
    private const int BlockBytes = 8192;

    private static readonly string[] DetailKeys = ["Hash Cond", "Join Filter", "Merge Cond", "Sort Key", "Group Key", "Index Cond", "Filter"];

    // Keys turned into dedicated PlanNode fields; everything else lands in Properties.
    private static readonly HashSet<string> Consumed = new(StringComparer.Ordinal)
    {
        "Node Type", "Relation Name", "Alias", "Index Name", "CTE Name", "Plans",
        "Startup Cost", "Total Cost", "Plan Rows", "Plan Width", "Actual Rows", "Actual Total Time", "Actual Loops",
    };

    /// <summary>Parses the JSON document returned by the server.</summary>
    /// <param name="statement">The explained statement.</param>
    /// <param name="mode">Estimated or actual.</param>
    /// <param name="json">The document exactly as PostgreSQL returned it.</param>
    /// <returns>The plan; <see cref="QueryPlan.RolledBack"/> is false, the reader sets it.</returns>
    /// <exception cref="PlanParseException">The text is not a PostgreSQL plan document.</exception>
    public static QueryPlan Parse(string statement, PlanMode mode, string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var top = document.RootElement;
            if (top.ValueKind == JsonValueKind.Array && top.GetArrayLength() > 0)
                top = top[0];
            if (top.ValueKind != JsonValueKind.Object || !top.TryGetProperty("Plan", out var plan) || plan.ValueKind != JsonValueKind.Object)
                throw new PlanParseException("The PostgreSQL plan document has no Plan object.", json);

            var sharedRead = 0.0;
            var hasBuffers = false;
            var root = Build(plan, ref sharedRead, ref hasBuffers);
            if (top.TryGetProperty("Triggers", out var triggers) && triggers.ValueKind == JsonValueKind.Array && triggers.GetArrayLength() > 0)
                root = root with { Properties = [.. root.Properties, new PlanProperty("Triggers", triggers.GetRawText())] };

            return new QueryPlan(
                DatabaseDialect.PostgreSql, mode, statement, PlanIds.Assign(root), [], [],
                Number(top, "Planning Time"), Number(top, "Execution Time"),
                hasBuffers ? (long)(sharedRead * BlockBytes) : null,
                false, PlanSourceFormat.Json, json);
        }
        catch (JsonException ex)
        {
            throw new PlanParseException("The PostgreSQL plan document is not valid JSON.", json, ex);
        }
    }

    private static PlanNode Build(JsonElement element, ref double sharedRead, ref bool hasBuffers)
    {
        var nodeType = Text(element, "Node Type") ?? "Unknown";
        var loops = Number(element, "Actual Loops");
        var actualRows = Number(element, "Actual Rows");
        var actualTime = Number(element, "Actual Total Time");
        var multiplier = loops ?? 1;

        if (Number(element, "Shared Read Blocks") is { } read)
        {
            sharedRead += read;
            hasBuffers = true;
        }

        string? detail = null;
        string? detailKey = null;
        foreach (var key in DetailKeys)
        {
            if (element.TryGetProperty(key, out var value) && Format(value) is { Length: > 0 } text)
            {
                detail = text;
                detailKey = key;
                break;
            }
        }

        var properties = new List<PlanProperty>();
        var warnings = new List<PlanWarning>();
        foreach (var property in element.EnumerateObject())
        {
            if (Consumed.Contains(property.Name) || property.Name == detailKey)
                continue;
            if (property.Name == "Rows Removed by Filter" && property.Value.TryGetDouble(out var removed))
                properties.Add(new PlanProperty(property.Name, (removed * multiplier).ToString("R", CultureInfo.InvariantCulture)));
            else
                properties.Add(new PlanProperty(property.Name, Format(property.Value)));
        }

        var sortMethod = Text(element, "Sort Method");
        if (string.Equals(Text(element, "Sort Space Type"), "Disk", StringComparison.OrdinalIgnoreCase)
            || sortMethod?.StartsWith("external", StringComparison.OrdinalIgnoreCase) == true)
            warnings.Add(new PlanWarning(PlanMessageCodes.SortSpill, [sortMethod ?? "", Number(element, "Sort Space Used")?.ToString("R", CultureInfo.InvariantCulture) ?? ""]));
        if (nodeType == "Hash" && Number(element, "Hash Batches") is > 1)
            warnings.Add(new PlanWarning(PlanMessageCodes.HashSpill, []));

        var children = new List<PlanNode>();
        if (element.TryGetProperty("Plans", out var plans) && plans.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in plans.EnumerateArray())
            {
                if (child.ValueKind == JsonValueKind.Object)
                    children.Add(Build(child, ref sharedRead, ref hasBuffers));
            }
        }

        return new PlanNode(
            0, nodeType, KindOf(nodeType, element), TargetOf(element), detail,
            Number(element, "Plan Rows"), Number(element, "Total Cost"), Number(element, "Startup Cost"),
            Number(element, "Plan Width") is { } width ? (int)width : null,
            actualRows * multiplier, actualTime * multiplier, loops is { } l ? (long)l : null,
            properties, warnings, nodeType == "Hash", children);
    }

    private static PlanNodeKind KindOf(string nodeType, JsonElement element) => nodeType switch
    {
        "Seq Scan" => PlanNodeKind.TableScan,
        "Index Scan" or "Bitmap Heap Scan" => PlanNodeKind.IndexScan,
        "Index Only Scan" or "Bitmap Index Scan" => element.TryGetProperty("Index Cond", out _) ? PlanNodeKind.IndexSeek : PlanNodeKind.IndexScan,
        "Nested Loop" or "Hash Join" or "Merge Join" => PlanNodeKind.Join,
        "Aggregate" or "HashAggregate" or "GroupAggregate" or "WindowAgg" => PlanNodeKind.Aggregate,
        "Sort" or "Incremental Sort" => PlanNodeKind.Sort,
        "Limit" => PlanNodeKind.Limit,
        "Hash" => PlanNodeKind.Helper,
        "Materialize" or "Memoize" => PlanNodeKind.Materialize,
        "CTE Scan" or "Subquery Scan" => PlanNodeKind.Subquery,
        "Append" or "Merge Append" or "SetOp" => PlanNodeKind.Union,
        "Gather" or "Gather Merge" => PlanNodeKind.Parallel,
        "Result" => PlanNodeKind.Result,
        _ => PlanNodeKind.Other,
    };

    private static string? TargetOf(JsonElement element)
    {
        var relation = Text(element, "Relation Name") ?? Text(element, "CTE Name");
        var alias = Text(element, "Alias");
        var table = relation is null ? alias : alias is null || alias == relation ? relation : relation + " " + alias;
        var index = Text(element, "Index Name");
        return table is null ? index : index is null ? table : table + " · " + index;
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static double? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) ? number : null;

    private static string Format(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? "",
        JsonValueKind.Array when value.EnumerateArray().All(e => e.ValueKind is JsonValueKind.String or JsonValueKind.Number) =>
            string.Join(", ", value.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : e.GetRawText())),
        _ => value.GetRawText(),
    };
}
