using System.Globalization;
using System.Text.Json;
using Squill.Core;
using Squill.Core.Plans;

namespace Squill.Data.MySql;

/// <summary>Turns the <c>EXPLAIN FORMAT=JSON</c> document of MySQL 8 into an engine-neutral <see cref="QueryPlan"/>; pure, never throws on unknown keys.</summary>
internal static class MySqlPlanParser
{
    // Keys that structure the tree; every other key of an object is shown in the details panel of the nearest node.
    private static readonly HashSet<string> Structural = new(StringComparer.Ordinal)
    {
        "query_block", "cost_info", "ordering_operation", "grouping_operation", "duplicates_removal", "windowing",
        "nested_loop", "table", "union_result", "query_specifications", "subqueries", "attached_subqueries", "materialized_from_subquery",
    };

    // Keys of a table object that become dedicated node fields.
    private static readonly HashSet<string> TableConsumed = new(StringComparer.Ordinal)
    {
        "table_name", "access_type", "key", "attached_condition", "rows_examined_per_scan", "cost_info", "subqueries", "attached_subqueries", "materialized_from_subquery",
    };

    /// <summary>Parses the JSON document returned by the server.</summary>
    /// <param name="statement">The explained statement.</param>
    /// <param name="json">The document exactly as MySQL returned it.</param>
    /// <returns>The estimated plan.</returns>
    /// <exception cref="PlanParseException">The text is not a MySQL plan document.</exception>
    public static QueryPlan Parse(string statement, string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("query_block", out var block) || block.ValueKind != JsonValueKind.Object)
                throw new PlanParseException("The MySQL plan document has no query_block object.", json);
            var root = Node("Query", PlanNodeKind.Result, null, null, Number(Cost(block), "query_cost"), null, Props(block, Structural), Content(block));
            return new QueryPlan(DatabaseDialect.MySql, PlanMode.Estimated, statement, PlanIds.Assign(root), [], [], null, null, null, false, PlanSourceFormat.Json, json);
        }
        catch (JsonException ex)
        {
            throw new PlanParseException("The MySQL plan document is not valid JSON.", json, ex);
        }
    }

    private static List<PlanNode> Content(JsonElement container)
    {
        var nodes = new List<PlanNode>();
        if (Wrapped(container, "ordering_operation", "Sort", PlanNodeKind.Sort, null) is { } sort)
            nodes.Add(sort);
        else if (Wrapped(container, "grouping_operation", "Aggregate", PlanNodeKind.Aggregate, null) is { } group)
            nodes.Add(group);
        else if (Wrapped(container, "duplicates_removal", "Aggregate", PlanNodeKind.Aggregate, "Distinct") is { } distinct)
            nodes.Add(distinct);
        else if (Wrapped(container, "windowing", "Window", PlanNodeKind.Aggregate, null) is { } window)
            nodes.Add(window);
        else if (container.TryGetProperty("nested_loop", out var loop) && loop.ValueKind == JsonValueKind.Array)
            nodes.Add(JoinChain(loop));
        else if (container.TryGetProperty("table", out var table) && table.ValueKind == JsonValueKind.Object)
            nodes.Add(Table(table));
        else if (container.TryGetProperty("union_result", out var union) && union.ValueKind == JsonValueKind.Object)
            nodes.Add(Union(union));
        nodes.AddRange(Subqueries(container, "subqueries", "Subquery"));
        nodes.AddRange(Subqueries(container, "attached_subqueries", "Subquery"));
        nodes.AddRange(Subqueries(container, "materialized_from_subquery", "Materialized subquery"));
        return nodes;
    }

    private static PlanNode? Wrapped(JsonElement container, string key, string operation, PlanNodeKind kind, string? detail) =>
        container.TryGetProperty(key, out var inner) && inner.ValueKind == JsonValueKind.Object
            ? Node(operation, kind, null, detail, null, null, Props(inner, Structural), Content(inner))
            : null;

    private static PlanNode JoinChain(JsonElement loop)
    {
        PlanNode? joined = null;
        foreach (var item in loop.EnumerateArray())
        {
            var side = One(Content(item), item);
            if (joined is null)
            {
                joined = side;
                continue;
            }
            var table = item.TryGetProperty("table", out var t) && t.ValueKind == JsonValueKind.Object ? t : default;
            joined = Node("Nested Loop", PlanNodeKind.Join, null, null, side.EstimatedCost, side.EstimatedRows, [], [joined, side]);
            if (table.ValueKind == JsonValueKind.Object && Number(table, "rows_produced_per_join") is { } produced)
                joined = joined with { EstimatedRows = produced };
        }
        return joined ?? Node("Nested Loop", PlanNodeKind.Join, null, null, null, null, [], []);
    }

    private static PlanNode Table(JsonElement table)
    {
        var access = Text(table, "access_type");
        var (operation, kind) = access switch
        {
            "ALL" => ("Table scan", PlanNodeKind.TableScan),
            "index" => ("Index scan", PlanNodeKind.IndexScan),
            "range" => ("Index range scan", PlanNodeKind.IndexSeek),
            "ref" or "eq_ref" or "ref_or_null" or "fulltext" or "index_merge" => ("Index lookup", PlanNodeKind.IndexSeek),
            "const" or "system" => ("Single-row lookup", PlanNodeKind.IndexSeek),
            _ => (access ?? "Table", PlanNodeKind.Other),
        };
        var name = Text(table, "table_name");
        var key = Text(table, "key");
        var target = name is null ? key : key is null ? name : name + " · " + key;
        var condition = Text(table, "attached_condition");
        var detail = condition ?? (table.TryGetProperty("used_key_parts", out var parts) ? Format(parts) : null);

        var properties = new List<PlanProperty>();
        foreach (var property in table.EnumerateObject())
        {
            if (TableConsumed.Contains(property.Name) || (condition is null && property.Name == "used_key_parts" && detail is not null))
                continue;
            properties.Add(new PlanProperty(property.Name, Format(property.Value)));
        }
        var cost = Cost(table);
        if (cost.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in cost.EnumerateObject().Where(p => p.Name != "prefix_cost"))
                properties.Add(new PlanProperty(property.Name, Format(property.Value)));
        }

        return Node(operation, kind, target, detail, Number(cost, "prefix_cost"), Number(table, "rows_examined_per_scan"), properties,
            [.. Subqueries(table, "subqueries", "Subquery"), .. Subqueries(table, "attached_subqueries", "Subquery"), .. Subqueries(table, "materialized_from_subquery", "Materialized subquery")]);
    }

    private static PlanNode Union(JsonElement union)
    {
        var children = new List<PlanNode>();
        if (union.TryGetProperty("query_specifications", out var specs) && specs.ValueKind == JsonValueKind.Array)
        {
            foreach (var spec in specs.EnumerateArray())
            {
                if (spec.TryGetProperty("query_block", out var block) && block.ValueKind == JsonValueKind.Object)
                    children.Add(One(Content(block), block));
            }
        }
        return Node("Union", PlanNodeKind.Union, null, null, null, null, Props(union, Structural), children);
    }

    private static IEnumerable<PlanNode> Subqueries(JsonElement container, string key, string operation)
    {
        if (!container.TryGetProperty(key, out var value))
            yield break;
        var items = value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToList() : [value];
        foreach (var item in items)
        {
            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("query_block", out var block) && block.ValueKind == JsonValueKind.Object)
                yield return Node(operation, PlanNodeKind.Subquery, null, null, Number(Cost(block), "query_cost"), null, Props(item, Structural), Content(block));
        }
    }

    /// <summary>Returns the only node of a block, or groups several under a <c>Query</c> node.</summary>
    private static PlanNode One(List<PlanNode> nodes, JsonElement block) =>
        nodes.Count == 1 ? nodes[0] : Node("Query", PlanNodeKind.Result, null, null, Number(Cost(block), "query_cost"), null, [], nodes);

    private static PlanNode Node(string operation, PlanNodeKind kind, string? target, string? detail, double? cost, double? rows, IReadOnlyList<PlanProperty> properties, IReadOnlyList<PlanNode> children) =>
        new(0, operation, kind, target, detail, rows, cost, null, null, null, null, null, properties, [], false, children);

    private static JsonElement Cost(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty("cost_info", out var cost) ? cost : default;

    private static List<PlanProperty> Props(JsonElement element, HashSet<string> skip) =>
        [.. element.EnumerateObject().Where(p => !skip.Contains(p.Name)).Select(p => new PlanProperty(p.Name, Format(p.Value)))];

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    // MySQL prints costs as strings ("12.50") and row counts as numbers; both parse with the invariant culture.
    private static double? Number(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
            return null;
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDouble(out var n) => n,
            JsonValueKind.String when double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) => s,
            _ => null,
        };
    }

    private static string Format(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? "",
        JsonValueKind.Array when value.EnumerateArray().All(e => e.ValueKind is JsonValueKind.String or JsonValueKind.Number) =>
            string.Join(", ", value.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : e.GetRawText())),
        _ => value.GetRawText(),
    };
}
