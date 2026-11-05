namespace Squill.Core;

/// <summary>The tables, views, routines and triggers of a database at a point in time.</summary>
/// <param name="DefaultSchema">Schema used for unqualified names, if the engine has one.</param>
/// <param name="Tables">Tables, views and materialized views.</param>
public sealed record SchemaSnapshot(string? DefaultSchema, IReadOnlyList<TableInfo> Tables)
{
    /// <summary>Stored functions and procedures (none on SQLite). Set with an object initializer; a default parameter cannot be a collection.</summary>
    public IReadOnlyList<RoutineInfo> Routines { get; init; } = [];

    /// <summary>Triggers of all tables. Set with an object initializer.</summary>
    public IReadOnlyList<TriggerInfo> Triggers { get; init; } = [];

    /// <summary>A snapshot with no tables.</summary>
    public static SchemaSnapshot Empty { get; } = new(null, []);

    /// <summary>Finds a table case-insensitively; without a schema, prefers the default schema, then a unique name.</summary>
    public TableInfo? FindTable(string? schema, string name)
    {
        var byName = Tables.Where(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (schema is not null)
            return byName.FirstOrDefault(t => string.Equals(t.Schema, schema, StringComparison.OrdinalIgnoreCase));
        if (DefaultSchema is not null
            && byName.FirstOrDefault(t => string.Equals(t.Schema, DefaultSchema, StringComparison.OrdinalIgnoreCase)) is { } def)
            return def;
        return byName.Count == 1 ? byName[0] : null;
    }
}
