using Squill.Core;
using Squill.Sql.Analysis;
using Squill.Sql.Syntax;

namespace Squill.Sql.Completion;

/// <summary>Builds the rows offered after <c>JOIN</c>: one per relation between a candidate table and the FROM list, with alias and <c>ON</c> condition.</summary>
internal static class JoinRows
{
    /// <summary>Builds the relation rows for a table position.</summary>
    /// <param name="schema">The schema snapshot.</param>
    /// <param name="join">The join context at the caret.</param>
    /// <param name="dialect">The dialect that decides the quoting.</param>
    /// <param name="range">The prefix being typed.</param>
    /// <param name="lowerKeywords">Whether <c>ON</c> and <c>AND</c> are written in lower case.</param>
    /// <returns>The rows, best first; empty when no condition is wanted or no relation exists.</returns>
    public static IReadOnlyList<CompletionItem> Build(SchemaSnapshot schema, JoinContext join, DatabaseDialect dialect, TextRange range, bool lowerKeywords = false)
    {
        if (!join.IsJoin || join.Kind is JoinKind.Cross or JoinKind.Comma || join.IsNatural || join.IsApply || join.HasCondition)
        {
            return [];
        }

        var left = join.LeftSources;
        var leftTables = new List<TableInfo>();
        foreach (var source in left)
        {
            if (source.DerivedColumns is null && schema.FindTable(source.Schema, source.Name) is { } table)
            {
                leftTables.Add(table);
            }
        }

        if (leftTables.Count == 0)
        {
            return [];
        }

        var leftNames = new HashSet<string>(leftTables.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);
        var leftPkNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var leftColumnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var table in leftTables)
        {
            foreach (var fk in table.ForeignKeys)
            {
                referenced.Add(fk.ReferencedTable);
            }

            foreach (var column in table.Columns)
            {
                leftColumnNames.Add(column.Name);
                if (column.IsPrimaryKey)
                {
                    leftPkNames.Add(column.Name);
                }
            }
        }

        var taken = new List<string>();
        foreach (var source in left)
        {
            taken.Add(source.Name);
            if (source.Alias is not null)
            {
                taken.Add(source.Alias);
            }
        }

        var found = new List<(TableRelation Relation, int Proximity, bool Repeat)>();
        foreach (var candidate in schema.Tables)
        {
            if (!MaybeRelated(candidate, leftNames, referenced, leftPkNames, leftColumnNames))
            {
                continue;
            }

            var repeat = leftTables.Any(t => ReferenceEquals(t, candidate));
            foreach (var relation in TableRelationFinder.Find(schema, left, candidate))
            {
                var proximity = 0;
                for (var i = 0; i < left.Count; i++)
                {
                    if (ReferenceEquals(left[i], relation.Existing))
                    {
                        proximity = i;
                    }
                }

                found.Add((relation, proximity, repeat));
            }
        }

        var ordered = found
            .OrderBy(f => f.Relation.Origin)
            .ThenByDescending(f => f.Proximity)
            .ThenBy(f => f.Repeat)
            .ThenBy(f => f.Relation.Other.Name, StringComparer.Ordinal)
            .ThenBy(f => f.Relation.ForeignKeyName, StringComparer.Ordinal)
            .ToList();

        var items = new List<CompletionItem>(ordered.Count);
        for (var rank = 0; rank < ordered.Count; rank++)
        {
            items.Add(Row(schema, left, ordered[rank].Relation, taken, dialect, range, rank, lowerKeywords));
        }

        return items;
    }

    private static bool MaybeRelated(TableInfo candidate, HashSet<string> leftNames, HashSet<string> referenced, HashSet<string> leftPkNames, HashSet<string> leftColumnNames)
    {
        if (referenced.Contains(candidate.Name) || candidate.ForeignKeys.Any(fk => leftNames.Contains(fk.ReferencedTable)))
        {
            return true;
        }

        return candidate.Columns.Any(c => leftPkNames.Contains(c.Name) || (c.IsPrimaryKey && leftColumnNames.Contains(c.Name)));
    }

    private static CompletionItem Row(SchemaSnapshot schema, IReadOnlyList<VisibleSource> left, TableRelation relation, List<string> taken, DatabaseDialect dialect, TextRange range, int rank, bool lowerKeywords)
    {
        var candidate = relation.Other;
        var existingTable = schema.FindTable(relation.Existing.Schema, relation.Existing.Name);
        var self = ReferenceEquals(existingTable, candidate);
        string? alias = AliasGenerator.IsAliasNeeded(left, candidate.Schema, candidate.Name, self)
            ? AliasGenerator.Generate(candidate.Name, taken, dialect)
            : null;
        var newName = alias ?? candidate.Name;
        var table = TableReference.Insert(candidate, schema, dialect);
        var label = alias is null ? table : table + " " + IdentifierQuoting.Quote(alias, dialect);
        var condition = JoinConditionText.Build(relation, relation.Existing.Alias ?? relation.Existing.Name, newName, dialect, lowerKeywords);
        var on = lowerKeywords ? "on " : "ON ";

        var documentation = CompletionDocumentation.Relation(schema, relation);

        return new CompletionItem(
            label,
            CompletionKind.Struct,
            label + " " + on + condition,
            on + condition,
            CompletionOrder.Relation + rank.ToString("D6", System.Globalization.CultureInfo.InvariantCulture),
            range,
            candidate.Name,
            documentation);
    }
}
