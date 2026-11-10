using System.Globalization;
using Squill.Core;
using Squill.Core.Plans;

namespace Squill.Data.MySql;

/// <summary>Turns the indented text tree of <c>EXPLAIN ANALYZE</c> into an engine-neutral <see cref="QueryPlan"/>; pure, a line it cannot read becomes an <c>Other</c> node.</summary>
internal static class MySqlAnalyzeTextParser
{
    private const int IndentWidth = 4;
    private const string ArrowPrefix = "-> ";
    private const string NeverExecuted = "(never executed)";
    private const string ActualMarker = "(actual time=";
    private const string CostMarker = "(cost=";

    /// <summary>Parses the tree returned by the server.</summary>
    /// <param name="statement">The analyzed statement.</param>
    /// <param name="text">The tree exactly as MySQL returned it.</param>
    /// <returns>The actual plan; MySQL prints costs and timings in the same tree, so it is complete.</returns>
    /// <exception cref="PlanParseException">The output is empty.</exception>
    public static QueryPlan Parse(string statement, string text)
    {
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Trim().Length > 0).ToList();
        if (lines.Count == 0)
            throw new PlanParseException("MySQL returned an empty EXPLAIN ANALYZE output.", text);

        var roots = new List<Builder>();
        var stack = new List<Builder>();
        foreach (var line in lines)
        {
            var depth = (line.Length - line.TrimStart(' ').Length) / IndentWidth;
            var builder = ParseLine(line.Trim());
            while (stack.Count > depth && stack.Count > 0)
                stack.RemoveAt(stack.Count - 1);
            if (stack.Count == 0)
                roots.Add(builder);
            else
                stack[^1].Children.Add(builder);
            stack.Add(builder);
        }

        var root = roots.Count == 1
            ? roots[0].ToNode()
            : new PlanNode(0, "Query", PlanNodeKind.Result, null, null, null, null, null, null, null, null, null, [], [], false, [.. roots.Select(r => r.ToNode())]);
        return new QueryPlan(DatabaseDialect.MySql, PlanMode.Actual, statement, PlanIds.Assign(root), [], [], null, root.ActualTimeMs, null, false, PlanSourceFormat.Text, text);
    }

    private static Builder ParseLine(string line)
    {
        if (!line.StartsWith(ArrowPrefix, StringComparison.Ordinal))
            return Builder.Junk(line);

        var body = line[ArrowPrefix.Length..];
        var never = body.EndsWith(NeverExecuted, StringComparison.Ordinal);
        double? actualRows = null, actualTime = null, loops = null;
        if (never)
        {
            body = body[..^NeverExecuted.Length].TrimEnd();
        }
        else if (body.LastIndexOf(ActualMarker, StringComparison.Ordinal) is var actualAt and >= 0 && ParseActual(body[(actualAt + 1)..], out var time, out var rows, out var loopCount))
        {
            (actualTime, actualRows, loops) = (time, rows, loopCount);
            body = body[..actualAt].TrimEnd();
        }

        double? cost = null, estimatedRows = null;
        if (body.LastIndexOf(CostMarker, StringComparison.Ordinal) is var costAt and >= 0 && body.EndsWith(')'))
        {
            (cost, estimatedRows) = ParseCost(body[(costAt + 1)..^1]);
            body = body[..costAt].TrimEnd();
        }

        var (operation, target, detail) = Split(body);
        var builder = new Builder(operation, KindOf(operation), target, detail, cost, estimatedRows);
        if (never)
        {
            (builder.ActualRows, builder.ActualTimeMs, builder.Loops) = (0, 0, 0);
            builder.Properties.Add(new PlanProperty("Never executed", "true"));
        }
        else if (loops is { } l)
        {
            (builder.ActualRows, builder.ActualTimeMs, builder.Loops) = (actualRows * l, actualTime * l, (long)l);
        }
        return builder;
    }

    // "actual time=0.261..4.56 rows=50000 loops=1)" (the text after the opening parenthesis): returns the last-row time, rows per loop and loops.
    private static bool ParseActual(string text, out double? time, out double? rows, out double? loops)
    {
        (time, rows, loops) = (null, null, null);
        if (!text.EndsWith(')'))
            return false;
        var parts = text[..^1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var eq = part.IndexOf('=', StringComparison.Ordinal);
            if (eq < 0)
                continue;
            var (key, value) = (part[..eq], part[(eq + 1)..]);
            switch (key)
            {
                case "time":
                    time = Number(value.Contains("..", StringComparison.Ordinal) ? value[(value.IndexOf("..", StringComparison.Ordinal) + 2)..] : value);
                    break;
                case "rows":
                    rows = Number(value);
                    break;
                case "loops":
                    loops = Number(value);
                    break;
            }
        }
        return time is not null && rows is not null && loops is not null;
    }

    private static (double? Cost, double? Rows) ParseCost(string text)
    {
        double? cost = null, rows = null;
        foreach (var part in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("cost=", StringComparison.Ordinal))
                cost = Number(part[5..]);
            else if (part.StartsWith("rows=", StringComparison.Ordinal))
                rows = Number(part[5..]);
        }
        return (cost, rows);
    }

    // "cost=0..0" (a node MySQL did not cost) keeps its first figure.
    private static double? Number(string text)
    {
        var dots = text.IndexOf("..", StringComparison.Ordinal);
        return double.TryParse(dots >= 0 ? text[..dots] : text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static (string Operation, string? Target, string? Detail) Split(string body)
    {
        var colon = body.IndexOf(": ", StringComparison.Ordinal);
        var on = body.IndexOf(" on ", StringComparison.Ordinal);
        var usingAt = body.IndexOf(" using ", StringComparison.Ordinal);
        var end = new[] { colon, on, usingAt }.Where(i => i >= 0).DefaultIfEmpty(-1).Min();
        if (end < 0)
            return (body, null, null);

        var operation = body[..end];
        if (end == colon)
            return (operation, null, body[(colon + 2)..].Trim());
        if (end == usingAt)
            return (operation, null, body[(usingAt + 1)..].Trim());

        var rest = body[(on + 4)..];
        var stops = new[] { rest.IndexOf(" using ", StringComparison.Ordinal), rest.IndexOf(" over ", StringComparison.Ordinal), rest.IndexOf(" (", StringComparison.Ordinal) }.Where(i => i >= 0).DefaultIfEmpty(-1).Min();
        return stops < 0 ? (operation, rest.Trim(), null) : (operation, rest[..stops].Trim(), rest[(stops + 1)..].Trim());
    }

    private static PlanNodeKind KindOf(string operation)
    {
        var text = operation.ToLowerInvariant();
        return text.Contains("join", StringComparison.Ordinal) ? PlanNodeKind.Join
            : text.Contains("sort", StringComparison.Ordinal) ? PlanNodeKind.Sort
            : text.Contains("aggregate", StringComparison.Ordinal) || text.Contains("group", StringComparison.Ordinal) ? PlanNodeKind.Aggregate
            : text.Contains("limit", StringComparison.Ordinal) ? PlanNodeKind.Limit
            : text.Contains("filter", StringComparison.Ordinal) ? PlanNodeKind.Filter
            : text.Contains("materialize", StringComparison.Ordinal) || text.Contains("temporary table", StringComparison.Ordinal) ? PlanNodeKind.Materialize
            : text.Contains("table scan", StringComparison.Ordinal) ? PlanNodeKind.TableScan
            : text.Contains("lookup", StringComparison.Ordinal) || text.Contains("range", StringComparison.Ordinal) ? PlanNodeKind.IndexSeek
            : text.Contains("index", StringComparison.Ordinal) ? PlanNodeKind.IndexScan
            : PlanNodeKind.Other;
    }

    /// <summary>Mutable node used while the tree is being assembled.</summary>
    private sealed class Builder(string operation, PlanNodeKind kind, string? target, string? detail, double? cost, double? rows)
    {
        public List<Builder> Children { get; } = [];

        public List<PlanProperty> Properties { get; } = [];

        public double? ActualRows { get; set; }

        public double? ActualTimeMs { get; set; }

        public long? Loops { get; set; }

        public static Builder Junk(string raw) => new(raw, PlanNodeKind.Other, null, null, null, null);

        public PlanNode ToNode() =>
            new(0, operation, kind, target, detail, rows, cost, null, null, ActualRows, ActualTimeMs, Loops, Properties, [], false, [.. Children.Select(c => c.ToNode())]);
    }
}
