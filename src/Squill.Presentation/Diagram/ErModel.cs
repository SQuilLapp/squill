using Squill.Core;

namespace Squill.Presentation.Diagram;

/// <summary>A column of an ER entity.</summary>
/// <param name="Name">Column name.</param>
/// <param name="DataType">Declared data type.</param>
/// <param name="IsPrimaryKey">Whether the column is part of the primary key.</param>
/// <param name="IsForeignKey">Whether the column is part of a foreign key.</param>
/// <param name="IsNullable">Whether the column accepts NULL.</param>
public sealed record ErColumn(string Name, string DataType, bool IsPrimaryKey, bool IsForeignKey, bool IsNullable);

/// <summary>A table or view drawn as a box of the ER diagram.</summary>
/// <param name="Key">Identity: the name, or <c>schema.name</c> when there is a schema; compared case-insensitively.</param>
/// <param name="Schema">Schema, or null when the dialect has none.</param>
/// <param name="Name">Table or view name.</param>
/// <param name="Kind">Whether this is a table or a view.</param>
/// <param name="Columns">Columns in declaration order.</param>
public sealed record ErEntity(string Key, string? Schema, string Name, TableKind Kind, IReadOnlyList<ErColumn> Columns);

/// <summary>How many child rows relate to one parent row.</summary>
public enum ErCardinality
{
    /// <summary>Many child rows reference one parent row.</summary>
    ManyToOne,

    /// <summary>At most one child row references a parent row.</summary>
    OneToOne,
}

/// <summary>A foreign key drawn as an edge from the child entity to the parent entity.</summary>
/// <param name="Name">Foreign key name.</param>
/// <param name="FromKey">Key of the child entity, which holds the foreign key.</param>
/// <param name="FromColumns">Foreign key columns of the child.</param>
/// <param name="ToKey">Key of the referenced (parent) entity.</param>
/// <param name="ToColumns">Referenced columns of the parent.</param>
/// <param name="Cardinality">One-to-one when the key columns are the child's primary key or a unique index.</param>
/// <param name="IsOptional">Whether every foreign key column is nullable.</param>
public sealed record ErRelation(
    string Name,
    string FromKey,
    IReadOnlyList<string> FromColumns,
    string ToKey,
    IReadOnlyList<string> ToColumns,
    ErCardinality Cardinality,
    bool IsOptional);

/// <summary>Selects which entities of a snapshot enter the ER model.</summary>
/// <param name="Keys">Entity keys to keep, or null for all.</param>
/// <param name="IncludeViews">Whether views are kept.</param>
public sealed record ErFilter(IReadOnlySet<string>? Keys, bool IncludeViews = false);

/// <summary>The entity-relationship model of a schema snapshot, ready to be laid out and drawn.</summary>
/// <param name="Entities">Entities ordered by schema then name.</param>
/// <param name="Relations">Resolved foreign keys.</param>
/// <param name="UnresolvedRelations">Foreign keys whose referenced table is not in the model.</param>
public sealed record ErModel(IReadOnlyList<ErEntity> Entities, IReadOnlyList<ErRelation> Relations, int UnresolvedRelations)
{
    /// <summary>Finds an entity by key, case-insensitively.</summary>
    /// <param name="key">Entity key.</param>
    /// <returns>The entity, or null.</returns>
    public ErEntity? Find(string key) =>
        Entities.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>Builds the model of a snapshot.</summary>
    /// <param name="snapshot">The schema snapshot.</param>
    /// <param name="filter">Optional entity filter; null keeps all tables and no views.</param>
    /// <returns>The model.</returns>
    public static ErModel Build(SchemaSnapshot snapshot, ErFilter? filter = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var tables = snapshot.Tables
            .Where(t => (filter?.IncludeViews == true || !t.Kind.IsViewLike())
                        && (filter?.Keys is not { } keys || keys.Contains(KeyOf(t.Schema, t.Name), StringComparer.OrdinalIgnoreCase)))
            .OrderBy(t => t.Schema, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var keySet = tables.Select(t => KeyOf(t.Schema, t.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<ErEntity> entities = [];
        List<ErRelation> relations = [];
        var unresolved = 0;
        foreach (var t in tables)
        {
            var fkColumns = t.ForeignKeys.SelectMany(f => f.Columns).ToHashSet(StringComparer.OrdinalIgnoreCase);
            entities.Add(new ErEntity(
                KeyOf(t.Schema, t.Name), t.Schema, t.Name, t.Kind,
                [.. t.Columns.Select(c => new ErColumn(c.Name, c.DataType, c.IsPrimaryKey, fkColumns.Contains(c.Name), c.IsNullable))]));
            if (t.Kind.IsViewLike())
                continue;

            foreach (var fk in t.ForeignKeys)
            {
                var parent = snapshot.FindTable(fk.ReferencedSchema ?? t.Schema, fk.ReferencedTable)
                             ?? (fk.ReferencedSchema is null ? snapshot.FindTable(null, fk.ReferencedTable) : null);
                var toKey = parent is null ? null : KeyOf(parent.Schema, parent.Name);
                if (parent is null || toKey is null || !keySet.Contains(toKey) || parent.Kind.IsViewLike())
                {
                    unresolved++;
                    continue;
                }

                var pk = t.Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name).ToList();
                var oneToOne = SameColumns(fk.Columns, pk)
                               || t.Indexes.Any(i => i.IsUnique && SameColumns(fk.Columns, i.Columns));
                var optional = fk.Columns.All(n => t.Columns.FirstOrDefault(
                    c => string.Equals(c.Name, n, StringComparison.OrdinalIgnoreCase))?.IsNullable == true);
                relations.Add(new ErRelation(
                    fk.Name, KeyOf(t.Schema, t.Name), fk.Columns, toKey, fk.ReferencedColumns,
                    oneToOne ? ErCardinality.OneToOne : ErCardinality.ManyToOne, optional));
            }
        }

        return new ErModel(entities, relations, unresolved);
    }

    /// <summary>Keys of the entities within a number of relation hops of an entity.</summary>
    /// <param name="model">The model.</param>
    /// <param name="key">Starting entity key.</param>
    /// <param name="depth">Maximum hops, following relations in both directions.</param>
    /// <returns>The keys, including <paramref name="key"/> itself.</returns>
    public static IReadOnlySet<string> Neighborhood(ErModel model, string key, int depth)
    {
        ArgumentNullException.ThrowIfNull(model);
        key = model.Find(key)?.Key ?? key;
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase) { key };
        var frontier = new List<string> { key };
        for (var i = 0; i < depth && frontier.Count > 0; i++)
        {
            List<string> next = [];
            foreach (var r in model.Relations)
            {
                if (frontier.Contains(r.FromKey, StringComparer.OrdinalIgnoreCase) && seen.Add(r.ToKey))
                    next.Add(r.ToKey);
                if (frontier.Contains(r.ToKey, StringComparer.OrdinalIgnoreCase) && seen.Add(r.FromKey))
                    next.Add(r.FromKey);
            }

            frontier = next;
        }

        return seen;
    }

    private static string KeyOf(string? schema, string name) => schema is null ? name : schema + "." + name;

    private static bool SameColumns(IReadOnlyList<string> a, IReadOnlyList<string> b) =>
        a.Count == b.Count && a.Count > 0
        && a.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(b);
}
