namespace Squill.Core.Design;

/// <summary>The editable, dialect-independent draft of a table that the designer edits and the DDL generators consume.</summary>
/// <param name="Schema">Schema name, if the engine has one.</param>
/// <param name="Name">Table name.</param>
/// <param name="Columns">Columns in order.</param>
/// <param name="PrimaryKey">Current names of the primary-key columns, in key order.</param>
/// <param name="PrimaryKeyName">Database name of the existing key constraint; null for a new key or when the engine has none.</param>
/// <param name="Indexes">Indexes of the table.</param>
/// <param name="ForeignKeys">Foreign keys of the table.</param>
public sealed record TableDesign(
    string? Schema,
    string Name,
    IReadOnlyList<ColumnDesign> Columns,
    IReadOnlyList<string> PrimaryKey,
    string? PrimaryKeyName,
    IReadOnlyList<IndexDesign> Indexes,
    IReadOnlyList<ForeignKeyDesign> ForeignKeys)
{
    /// <summary>Creates a design that mirrors an existing table, with every original name equal to the current one.</summary>
    /// <param name="table">The table to copy.</param>
    /// <returns>The draft.</returns>
    /// <exception cref="ArgumentException"><paramref name="table"/> is a view.</exception>
    public static TableDesign FromTable(TableInfo table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (table.Kind != TableKind.Table)
        {
            throw new ArgumentException("A view cannot be designed.", nameof(table));
        }

        return new TableDesign(
            table.Schema,
            table.Name,
            [.. table.Columns.Select(c => new ColumnDesign(c.Name, c.Name, c.DataType, c.IsNullable, c.IsAutoIncrement ? null : c.DefaultExpression, c.IsAutoIncrement))],
            [.. table.Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name)],
            table.PrimaryKeyName,
            [.. table.Indexes.Select(i => new IndexDesign(i.Name, i.Name, i.IsUnique, i.Columns))],
            [.. table.ForeignKeys.Select(f => new ForeignKeyDesign(f.Name, f.Name, f.Columns, f.ReferencedSchema, f.ReferencedTable, f.ReferencedColumns, f.OnDelete, f.OnUpdate))]);
    }

    /// <summary>Creates an empty design for a table to be created.</summary>
    /// <param name="schema">Schema name, if any.</param>
    /// <param name="name">Table name.</param>
    /// <returns>A design with no column, key, index or foreign key.</returns>
    public static TableDesign New(string? schema, string name) => new(schema, name, [], [], null, [], []);
}
