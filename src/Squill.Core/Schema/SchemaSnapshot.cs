namespace Squill.Core;

/// <summary>The tables and views of a database at a point in time.</summary>
public sealed record SchemaSnapshot(string? DefaultSchema, IReadOnlyList<TableInfo> Tables)
{
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
