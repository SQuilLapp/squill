using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Squill.Core;
using Squill.Core.Plans;

namespace Squill.Data.SqlServer;

/// <summary>Turns a SQL Server showplan XML document into an engine-neutral <see cref="QueryPlan"/>; pure, never throws on unknown attributes.</summary>
internal static class SqlServerPlanParser
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/sqlserver/2004/07/showplan";

    // RelOp attributes shown in the details panel when present.
    private static readonly string[] PropertyAttributes =
        ["EstimateIO", "EstimateCPU", "EstimateRebinds", "EstimateRewinds", "Parallel", "TableCardinality", "EstimatedExecutionMode"];

    // Elements that hold a predicate; the first scalar string below each is the node detail.
    private static readonly HashSet<string> PredicateElements = new(StringComparer.Ordinal) { "Predicate", "SeekPredicateNew", "SeekPredicate", "Residual", "ProbeResidual" };

    /// <summary>Parses the XML document returned by <c>SET SHOWPLAN_XML ON</c>.</summary>
    /// <param name="statement">The explained statement.</param>
    /// <param name="xml">The document exactly as SQL Server returned it.</param>
    /// <returns>The estimated plan; only the first statement of the batch is read.</returns>
    /// <exception cref="PlanParseException">The text is not a showplan document.</exception>
    public static QueryPlan Parse(string statement, string xml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (XmlException ex)
        {
            throw new PlanParseException("The SQL Server plan document is not valid XML.", xml, ex);
        }

        var statements = document.Descendants(Ns + "StmtSimple").Where(s => s.Element(Ns + "QueryPlan")?.Element(Ns + "RelOp") is not null).ToList();
        if (document.Root?.Name != Ns + "ShowPlanXML" || statements.Count == 0)
            throw new PlanParseException("The SQL Server plan document has no statement with an operator tree.", xml);

        var queryPlan = statements[0].Element(Ns + "QueryPlan")!;
        var root = Node(queryPlan.Element(Ns + "RelOp")!);
        if (statements.Count > 1)
            root = root with { Properties = [.. root.Properties, new PlanProperty("Other statements", (statements.Count - 1).ToString(CultureInfo.InvariantCulture))] };

        var warnings = Warnings(queryPlan.Element(Ns + "Warnings"), "");
        var suggestions = new List<PlanSuggestion>();
        foreach (var group in queryPlan.Elements(Ns + "MissingIndexes").Elements(Ns + "MissingIndexGroup"))
        {
            foreach (var index in group.Elements(Ns + "MissingIndex"))
            {
                if (Suggestion(index) is { } suggestion)
                {
                    suggestions.Add(suggestion);
                    warnings.Add(new PlanWarning(PlanMessageCodes.MissingIndex, [(string?)group.Attribute("Impact") ?? ""]));
                }
            }
        }

        double? planning = double.TryParse((string?)queryPlan.Attribute("CompileTime"), NumberStyles.Float, CultureInfo.InvariantCulture, out var compile) ? compile : null;
        return new QueryPlan(DatabaseDialect.SqlServer, PlanMode.Estimated, statement, PlanIds.Assign(root), warnings, suggestions, planning, null, null, false, PlanSourceFormat.Xml, document.ToString());
    }

    private static PlanNode Node(XElement relOp)
    {
        var physical = (string?)relOp.Attribute("PhysicalOp") ?? "";
        var logical = (string?)relOp.Attribute("LogicalOp") ?? "";
        var own = Own(relOp).ToList();
        var properties = new List<PlanProperty>();
        foreach (var name in PropertyAttributes)
        {
            if ((string?)relOp.Attribute(name) is { } value)
                properties.Add(new PlanProperty(name, value));
        }

        if (own.Select(e => (string?)e.Attribute("Ordered")).FirstOrDefault(v => v is not null) is { } ordered)
            properties.Add(new PlanProperty("Ordered", ordered));

        return new PlanNode(
            0, physical, Kind(physical, logical), Target(own), Detail(own), Number(relOp, "EstimateRows"), Number(relOp, "EstimatedTotalSubtreeCost"), null,
            Number(relOp, "AvgRowSize") is { } width ? (int)width : null, null, null, null, properties, Warnings(relOp.Element(Ns + "Warnings"), physical),
            false, [.. Children(relOp).Select(Node)]);
    }

    // The RelOp descendants whose nearest RelOp ancestor is relOp: operator wrappers (NestedLoops, Hash, Sort...) sit in between.
    private static IEnumerable<XElement> Children(XElement relOp) =>
        relOp.Descendants(Ns + "RelOp").Where(d => d.Ancestors(Ns + "RelOp").First() == relOp);

    // Descendants that belong to this operator and not to a child operator.
    private static IEnumerable<XElement> Own(XElement relOp) =>
        relOp.Descendants().Where(d => d.Ancestors(Ns + "RelOp").First() == relOp);

    private static double? Number(XElement element, string attribute) =>
        double.TryParse((string?)element.Attribute(attribute), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static PlanNodeKind Kind(string physical, string logical) => physical switch
    {
        "Table Scan" or "Clustered Index Scan" => PlanNodeKind.TableScan,
        "Index Scan" or "Columnstore Index Scan" => PlanNodeKind.IndexScan,
        "Index Seek" or "Clustered Index Seek" or "RID Lookup" or "Key Lookup" => PlanNodeKind.IndexSeek,
        "Nested Loops" or "Merge Join" or "Adaptive Join" => PlanNodeKind.Join,
        "Hash Match" or "Stream Aggregate" => logical.Contains("Join", StringComparison.Ordinal) ? PlanNodeKind.Join
            : logical.Contains("Aggregate", StringComparison.Ordinal) || physical == "Stream Aggregate" ? PlanNodeKind.Aggregate : PlanNodeKind.Other,
        "Sort" or "Top N Sort" or "Distinct Sort" => PlanNodeKind.Sort,
        "Top" => PlanNodeKind.Limit,
        "Filter" => PlanNodeKind.Filter,
        "Table Spool" or "Index Spool" or "Row Count Spool" => PlanNodeKind.Materialize,
        "Concatenation" or "Merge Interval" => PlanNodeKind.Union,
        "Parallelism" => PlanNodeKind.Parallel,
        _ => PlanNodeKind.Other,
    };

    private static string? Target(List<XElement> own)
    {
        if (own.FirstOrDefault(e => e.Name == Ns + "Object") is not { } obj)
            return null;
        var name = $"{(string?)obj.Attribute("Schema")}.{(string?)obj.Attribute("Table")}".Trim('.');
        if ((string?)obj.Attribute("Alias") is { } alias)
            name += " " + alias;
        return (string?)obj.Attribute("Index") is { } index ? name + " · " + index : name;
    }

    private static string? Detail(List<XElement> own)
    {
        var parts = new List<string>();
        foreach (var element in own.Where(e => PredicateElements.Contains(e.Name.LocalName)))
        {
            if (element.Descendants(Ns + "ScalarOperator").Select(s => (string?)s.Attribute("ScalarString")).FirstOrDefault(s => s is not null) is { } text)
                parts.Add(text);
        }

        if (parts.Count == 0)
        {
            foreach (var element in own.Where(e => e.Name.LocalName is "OrderBy" or "HashKeysBuild"))
                parts.Add(string.Join(", ", element.Descendants(Ns + "ColumnReference").Select(c => (string?)c.Attribute("Column")).Where(c => c is not null)));
        }

        return parts.Count == 0 ? null : string.Join(" AND ", parts);
    }

    private static List<PlanWarning> Warnings(XElement? warnings, string physical)
    {
        var result = new List<PlanWarning>();
        if (warnings is null)
            return result;
        foreach (var warning in warnings.Elements())
        {
            switch (warning.Name.LocalName)
            {
                case "SpillToTempDb":
                    result.Add(new PlanWarning(physical == "Sort" ? PlanMessageCodes.SortSpill : PlanMessageCodes.HashSpill, []));
                    break;
                case "ColumnsWithNoStatistics":
                    result.Add(new PlanWarning(PlanMessageCodes.NoStats, [string.Join(", ", warning.Elements().Select(c => (string?)c.Attribute("Column")).Where(c => c is not null))]));
                    break;
                case "PlanAffectingConvert":
                    result.Add(new PlanWarning(PlanMessageCodes.ImplicitConversion, [(string?)warning.Attribute("Expression") ?? ""]));
                    break;
            }
        }

        if ((string?)warnings.Attribute("NoJoinPredicate") is "true" or "1")
            result.Add(new PlanWarning(PlanMessageCodes.NoJoinPredicate, []));
        return result;
    }

    private static PlanSuggestion? Suggestion(XElement index)
    {
        List<string> Columns(string usage) =>
            [.. index.Elements(Ns + "ColumnGroup").Where(g => (string?)g.Attribute("Usage") == usage).Elements(Ns + "Column").Select(c => (string?)c.Attribute("Name") ?? "")];

        var keys = Columns("EQUALITY").Concat(Columns("INEQUALITY")).ToList();
        if (keys.Count == 0)
            return null;
        var table = (string?)index.Attribute("Table") ?? "";
        var name = "IX_" + table.Trim('[', ']') + "_" + string.Join('_', keys.Select(k => k.Trim('[', ']')));
        name = name[..Math.Min(name.Length, 128)];
        var include = Columns("INCLUDE");
        var text = $"CREATE INDEX [{name}] ON {(string?)index.Attribute("Schema")}.{table} ({string.Join(", ", keys)})"
            + (include.Count > 0 ? $" INCLUDE ({string.Join(", ", include)})" : "") + ";";
        return new PlanSuggestion("Missing index", text);
    }
}
