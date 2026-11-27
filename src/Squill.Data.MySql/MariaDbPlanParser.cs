using System.Text.Json;
using Squill.Core;
using Squill.Core.Plans;
using static Squill.Data.MySql.MySqlPlanParser;

namespace Squill.Data.MySql;

/// <summary>Turns the <c>EXPLAIN FORMAT=JSON</c> or <c>ANALYZE FORMAT=JSON</c> document of MariaDB (tested on 11.4) into an engine-neutral <see cref="QueryPlan"/>; pure, never throws on unknown keys.</summary>
/// <remarks>MariaDB has no <c>cost_info</c>: the <c>cost</c> of a table is its own cost, not a cumulative one. A figure the server does not give stays <see langword="null"/>.</remarks>
internal static class MariaDbPlanParser
{
    // Keys that structure the tree; every other key of an object is shown in the details panel of the nearest node.
    private static readonly HashSet<string> Structural = new(StringComparer.Ordinal)
    {
        "query_block", "query_optimization", "nested_loop", "table", "block-nl-join", "filesort", "temporary_table", "duplicate_removal", "window_functions_computation",
        "union_result", "query_specifications", "subqueries", "materialized", "cost", "r_loops", "r_total_time_ms",
    };

    // Keys of a table object that become dedicated node fields.
    private static readonly HashSet<string> TableConsumed = new(StringComparer.Ordinal)
    {
        "table_name", "access_type", "key", "attached_condition", "rows", "r_rows", "cost", "r_loops", "r_table_time_ms", "r_other_time_ms", "materialized",
    };

    // Wrappers of a block, outermost first: key, operator name, kind, detail.
    private static readonly (string Key, string Operation, PlanNodeKind Kind, string? Detail)[] Wrappers =
    [
        ("filesort", "Sort", PlanNodeKind.Sort, null),
        ("duplicate_removal", "Aggregate", PlanNodeKind.Aggregate, "Distinct"),
        ("window_functions_computation", "Window", PlanNodeKind.Aggregate, null),
        ("temporary_table", "Temporary table", PlanNodeKind.Other, null),
    ];

    /// <summary>Parses the JSON document returned by the server.</summary>
    /// <param name="statement">The explained statement.</param>
    /// <param name="json">The document exactly as MariaDB returned it.</param>
    /// <param name="mode">Estimated (<c>EXPLAIN</c>) or actual (<c>ANALYZE</c>).</param>
    /// <returns>The plan.</returns>
    /// <exception cref="PlanParseException">The text is not a MariaDB plan document.</exception>
    public static QueryPlan Parse(string statement, string json, PlanMode mode)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("query_block", out var block) || block.ValueKind != JsonValueKind.Object)
                throw new PlanParseException("The MariaDB plan document has no query_block object.", json);
            var (_, time, loops) = Actuals(block);
            var root = Node("Query", PlanNodeKind.Result, null, null, Number(block, "cost"), null, Props(block, Structural), Content(block)) with { ActualTimeMs = time, Loops = loops };
            double? planning = document.RootElement.TryGetProperty("query_optimization", out var optimization) ? Number(optimization, "r_total_time_ms") : null;
            return new QueryPlan(DatabaseDialect.MySql, mode, statement, PlanIds.Assign(root), [], [], planning, time, null, false, PlanSourceFormat.Json, json);
        }
        catch (JsonException ex)
        {
            throw new PlanParseException("The MariaDB plan document is not valid JSON.", json, ex);
        }
    }

    // r_rows and r_*_time_ms are per loop and total over all loops: the plan wants rows totaled over the loops.
    private static (double? Rows, double? TimeMs, long? Loops) Actuals(JsonElement element, string rows = "r_rows")
    {
        var loops = Number(element, "r_loops");
        var perLoop = Number(element, rows);
        double? time = Number(element, "r_total_time_ms");
        if (Number(element, "r_table_time_ms") is { } table)
            time = table + (Number(element, "r_other_time_ms") ?? 0);
        return (perLoop is { } r ? r * (loops ?? 1) : null, time, loops is { } l ? (long)l : null);
    }

    private static List<PlanNode> Content(JsonElement container)
    {
        var nodes = new List<PlanNode>();
        foreach (var (key, operation, kind, detail) in Wrappers)
        {
            if (container.TryGetProperty(key, out var inner) && inner.ValueKind == JsonValueKind.Object)
            {
                var (rows, time, loops) = Actuals(inner, "r_output_rows");
                nodes.Add(Node(operation, kind, null, detail, null, null, Props(inner, Structural), Content(inner)) with { ActualRows = rows, ActualTimeMs = time, Loops = loops });
                break;
            }
        }

        if (nodes.Count == 0)
        {
            if (container.TryGetProperty("nested_loop", out var loop) && loop.ValueKind == JsonValueKind.Array)
                nodes.Add(JoinChain(loop));
            else if (container.TryGetProperty("table", out var table) && table.ValueKind == JsonValueKind.Object)
                nodes.Add(Table(table));
            else if (container.TryGetProperty("union_result", out var union) && union.ValueKind == JsonValueKind.Object)
                nodes.Add(Union(union));
        }

        nodes.AddRange(Subqueries(container));
        return nodes;
    }

    private static PlanNode JoinChain(JsonElement loop)
    {
        PlanNode? joined = null;
        foreach (var item in loop.EnumerateArray())
        {
            // A joined table with a join buffer is wrapped in "block-nl-join" next to the join's own keys.
            var wrapper = item.ValueKind == JsonValueKind.Object && item.TryGetProperty("block-nl-join", out var buffered) && buffered.ValueKind == JsonValueKind.Object ? buffered : default;
            var holder = wrapper.ValueKind == JsonValueKind.Object ? wrapper : item;
            if (holder.ValueKind != JsonValueKind.Object || !holder.TryGetProperty("table", out var table) || table.ValueKind != JsonValueKind.Object)
                continue;
            var side = Table(table);
            if (joined is null)
            {
                joined = side;
                continue;
            }
            var properties = wrapper.ValueKind == JsonValueKind.Object ? Props(wrapper, Structural) : [];
            var detail = wrapper.ValueKind == JsonValueKind.Object ? Text(wrapper, "attached_condition") : null;
            joined = Node(wrapper.ValueKind == JsonValueKind.Object ? "Block nested loop" : "Nested loop", PlanNodeKind.Join, null, detail, null, null, properties, [joined, side]);
        }
        return joined ?? Node("Nested loop", PlanNodeKind.Join, null, null, null, null, [], []);
    }

    private static PlanNode Table(JsonElement table)
    {
        var (operation, kind) = Access(Text(table, "access_type"));
        var name = Text(table, "table_name");
        var key = Text(table, "key");
        var target = name is null ? key : key is null ? name : name + " · " + key;
        var condition = Text(table, "attached_condition");
        var detail = condition ?? (table.TryGetProperty("used_key_parts", out var parts) ? Format(parts) : null);
        var properties = new List<PlanProperty>();
        foreach (var property in table.EnumerateObject())
        {
            if (!TableConsumed.Contains(property.Name) && !(condition is null && property.Name == "used_key_parts" && detail is not null))
                properties.Add(new PlanProperty(property.Name, Format(property.Value)));
        }

        var (rows, time, loops) = Actuals(table);
        return Node(operation, kind, target, detail, Number(table, "cost"), Number(table, "rows"), properties, [.. Subqueries(table)]) with { ActualRows = rows, ActualTimeMs = time, Loops = loops };
    }

    private static PlanNode Union(JsonElement union)
    {
        var children = new List<PlanNode>();
        if (union.TryGetProperty("query_specifications", out var specs) && specs.ValueKind == JsonValueKind.Array)
        {
            foreach (var spec in specs.EnumerateArray())
            {
                if (spec.TryGetProperty("query_block", out var block) && block.ValueKind == JsonValueKind.Object)
                    children.Add(Block(block, "Query"));
            }
        }
        return Node("Union", PlanNodeKind.Union, null, null, null, null, Props(union, new HashSet<string>(["query_specifications"], StringComparer.Ordinal)), children);
    }

    // Subqueries sit in a "subqueries" array, each in a cache object ("subquery_cache") that holds the query_block.
    private static IEnumerable<PlanNode> Subqueries(JsonElement container)
    {
        if (!container.TryGetProperty("subqueries", out var items) || items.ValueKind != JsonValueKind.Array)
            yield break;
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            var block = item.TryGetProperty("query_block", out var direct) ? direct
                : item.EnumerateObject().Select(p => p.Value).FirstOrDefault(v => v.ValueKind == JsonValueKind.Object && v.TryGetProperty("query_block", out _)) is { ValueKind: JsonValueKind.Object } wrapped
                    ? wrapped.GetProperty("query_block") : default;
            if (block.ValueKind == JsonValueKind.Object)
                yield return Block(block, "Subquery") with { Kind = PlanNodeKind.Subquery };
        }
    }

    private static PlanNode Block(JsonElement block, string operation)
    {
        var nodes = Content(block);
        var (_, time, loops) = Actuals(block);
        return Node(operation, PlanNodeKind.Result, null, null, Number(block, "cost"), null, Props(block, Structural), nodes) with { ActualTimeMs = time, Loops = loops };
    }
}
