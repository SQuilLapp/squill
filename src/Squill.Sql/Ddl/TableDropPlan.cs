using Squill.Core;

namespace Squill.Sql.Ddl;

/// <summary>A table outside a drop selection that references a selected table, so the engine would refuse the drop.</summary>
/// <param name="Referencing">The table that is not selected and owns the foreign key.</param>
/// <param name="Referenced">The selected table the foreign key points to.</param>
public sealed record DropBlocker(TableInfo Referencing, TableInfo Referenced);

/// <summary>The order in which a set of tables must be dropped and the tables that would block the drop.</summary>
/// <param name="Order">Tables to drop, each one before the tables it references.</param>
/// <param name="Blockers">Foreign keys from unselected tables to selected ones; <c>CASCADE</c> is never used.</param>
/// <param name="HasCycle">True when selected tables reference each other in a loop; the loop members are appended in selection order.</param>
public sealed record TableDropPlan(IReadOnlyList<TableInfo> Order, IReadOnlyList<DropBlocker> Blockers, bool HasCycle)
{
    /// <summary>Computes the drop order and the blockers of a selection.</summary>
    /// <param name="selected">The tables to drop; duplicates are removed, the first one wins.</param>
    /// <param name="schema">The schema the selection was made from.</param>
    /// <returns>The plan.</returns>
    /// <exception cref="ArgumentException">A selected item is not a base table.</exception>
    public static TableDropPlan Build(IReadOnlyList<TableInfo> selected, SchemaSnapshot schema)
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(schema);

        var tables = new List<TableInfo>();
        var index = new Dictionary<string, int>();
        foreach (var t in selected)
        {
            if (t.Kind != TableKind.Table)
                throw new ArgumentException($"'{t.Name}' is not a base table.", nameof(selected));
            if (index.TryAdd(Key(t.Schema, t.Name), tables.Count))
                tables.Add(t);
        }

        // referencing -> referenced, only between selected tables, self references ignored.
        var blockedBy = new HashSet<int>[tables.Count]; // blockedBy[i] = selected tables that reference i
        for (var i = 0; i < tables.Count; i++)
            blockedBy[i] = [];

        for (var i = 0; i < tables.Count; i++)
            foreach (var fk in tables[i].ForeignKeys)
                if (index.TryGetValue(Key(fk.ReferencedSchema ?? tables[i].Schema, fk.ReferencedTable), out var j) && j != i)
                    blockedBy[j].Add(i);

        var order = new List<TableInfo>();
        var done = new bool[tables.Count];
        while (true)
        {
            var next = -1;
            for (var i = 0; i < tables.Count && next < 0; i++)
                if (!done[i] && blockedBy[i].All(r => done[r]))
                    next = i;
            if (next < 0)
                break;
            done[next] = true;
            order.Add(tables[next]);
        }

        var hasCycle = order.Count < tables.Count;
        for (var i = 0; i < tables.Count; i++)
            if (!done[i])
                order.Add(tables[i]);

        var blockers = new List<DropBlocker>();
        foreach (var other in schema.Tables.OrderBy(t => t.Schema, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (index.ContainsKey(Key(other.Schema, other.Name)))
                continue;
            var seen = new HashSet<int>();
            foreach (var fk in other.ForeignKeys)
                if (index.TryGetValue(Key(fk.ReferencedSchema ?? other.Schema, fk.ReferencedTable), out var j) && seen.Add(j))
                    blockers.Add(new DropBlocker(other, tables[j]));
        }

        return new TableDropPlan(order, blockers, hasCycle);
    }

    // shortcut: a PostgreSQL schema holding two tables that differ only by case collapses to one key, upgrade when such a schema must be supported.
    private static string Key(string? schema, string name) => $"{schema?.ToUpperInvariant()}\0{name.ToUpperInvariant()}";
}
