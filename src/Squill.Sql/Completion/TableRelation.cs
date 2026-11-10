using Squill.Core;
using Squill.Sql.Analysis;

namespace Squill.Sql.Completion;

/// <summary>Where a <see cref="TableRelation"/> comes from.</summary>
internal enum RelationOrigin
{
    /// <summary>A declared foreign key.</summary>
    ForeignKey,

    /// <summary>A column of the same name that is a primary-key column of one of the two tables.</summary>
    SameColumnName,
}

/// <summary>One way to join a candidate table to a table already in the FROM list.</summary>
/// <param name="Existing">The source already in the FROM list.</param>
/// <param name="Other">The candidate table.</param>
/// <param name="ExistingColumns">Columns of the existing table, in key order.</param>
/// <param name="OtherColumns">Columns of the candidate, paired by index with <paramref name="ExistingColumns"/>.</param>
/// <param name="Origin">Where the relation comes from.</param>
/// <param name="ForeignKeyName">The constraint name, or <see langword="null"/> for a same-name relation.</param>
/// <param name="OtherReferencesExisting">Whether the candidate holds the foreign key (or the candidate's columns are the non-key side).</param>
internal sealed record TableRelation(
    VisibleSource Existing,
    TableInfo Other,
    IReadOnlyList<string> ExistingColumns,
    IReadOnlyList<string> OtherColumns,
    RelationOrigin Origin,
    string? ForeignKeyName,
    bool OtherReferencesExisting);

/// <summary>Finds the relations between a candidate table and the sources already in the FROM list.</summary>
internal static class TableRelationFinder
{
    /// <summary>Finds every relation of <paramref name="candidate"/> with the real tables of <paramref name="leftSources"/>.</summary>
    /// <param name="schema">The schema snapshot.</param>
    /// <param name="leftSources">The sources already in the FROM list, in order; derived tables and CTEs are skipped.</param>
    /// <param name="candidate">The table that would be joined.</param>
    /// <returns>Foreign-key relations first, else same-name relations; nearest source first.</returns>
    public static IReadOnlyList<TableRelation> Find(SchemaSnapshot schema, IReadOnlyList<VisibleSource> leftSources, TableInfo candidate)
    {
        var found = new List<(int Index, TableRelation Relation)>();
        var tables = new (int Index, VisibleSource Source, TableInfo Table)[leftSources.Count];
        var count = 0;
        for (var i = 0; i < leftSources.Count; i++)
        {
            if (leftSources[i].DerivedColumns is null && schema.FindTable(leftSources[i].Schema, leftSources[i].Name) is { } table)
            {
                tables[count++] = (i, leftSources[i], table);
            }
        }

        foreach (var (index, source, table) in tables.Take(count))
        {
            foreach (var fk in candidate.ForeignKeys)
            {
                if (Matches(fk, table) && fk.Columns.Count == fk.ReferencedColumns.Count)
                {
                    found.Add((index, new TableRelation(source, candidate, fk.ReferencedColumns, fk.Columns, RelationOrigin.ForeignKey, fk.Name, true)));
                }
            }

            foreach (var fk in table.ForeignKeys)
            {
                if (Matches(fk, candidate) && fk.Columns.Count == fk.ReferencedColumns.Count)
                {
                    found.Add((index, new TableRelation(source, candidate, fk.Columns, fk.ReferencedColumns, RelationOrigin.ForeignKey, fk.Name, false)));
                }
            }
        }

        if (found.Count == 0)
        {
            foreach (var (index, source, table) in tables.Take(count))
            {
                foreach (var relation in SameName(source, table, candidate))
                {
                    found.Add((index, relation));
                }
            }
        }

        return [.. found
            .OrderBy(f => f.Relation.Origin)
            .ThenByDescending(f => f.Index)
            .ThenBy(f => f.Relation.ForeignKeyName is null ? 1 : 0)
            .ThenBy(f => f.Relation.ForeignKeyName, StringComparer.Ordinal)
            .ThenByDescending(f => f.Relation.OtherReferencesExisting)
            .Select(f => f.Relation)];
    }

    private static bool Matches(ForeignKeyInfo fk, TableInfo target) =>
        string.Equals(fk.ReferencedTable, target.Name, StringComparison.OrdinalIgnoreCase)
        && (fk.ReferencedSchema is null || string.Equals(fk.ReferencedSchema, target.Schema, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<TableRelation> SameName(VisibleSource source, TableInfo existing, TableInfo candidate)
    {
        if (ReferenceEquals(existing, candidate))
        {
            yield break;
        }

        var existingKey = KeyColumnsIn(existing, candidate);
        if (existingKey is { } e)
        {
            yield return new TableRelation(source, candidate, e.Key, e.Other, RelationOrigin.SameColumnName, null, true);
        }

        if (KeyColumnsIn(candidate, existing) is { } c
            && !(existingKey is { } same && same.Key.Count == c.Key.Count && same.Key.All(k => c.Other.Contains(k, StringComparer.OrdinalIgnoreCase))))
        {
            yield return new TableRelation(source, candidate, c.Other, c.Key, RelationOrigin.SameColumnName, null, false);
        }
    }

    /// <summary>The primary key of <paramref name="owner"/> and the same-named columns of <paramref name="other"/>, when every key column exists there.</summary>
    private static (IReadOnlyList<string> Key, IReadOnlyList<string> Other)? KeyColumnsIn(TableInfo owner, TableInfo other)
    {
        var key = owner.Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name).ToList();
        if (key.Count == 0 || (key.Count == 1 && key[0].Equals("id", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var match = new List<string>(key.Count);
        foreach (var name in key)
        {
            var column = other.Columns.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (column is null)
            {
                return null;
            }

            match.Add(column.Name);
        }

        return (key, match);
    }
}
