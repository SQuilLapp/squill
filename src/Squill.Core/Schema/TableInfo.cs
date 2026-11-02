namespace Squill.Core;

/// <summary>A table or view with its columns, indexes and foreign keys.</summary>
/// <param name="Schema">Schema name, if the engine has one.</param>
/// <param name="Name">Table or view name.</param>
/// <param name="Kind">Whether this is a table or a view.</param>
/// <param name="Columns">Columns in ordinal order.</param>
/// <param name="Indexes">Indexes of the table.</param>
/// <param name="ForeignKeys">Foreign keys of the table.</param>
/// <param name="PrimaryKeyName">Primary-key constraint name; null when the engine has no meaningful name or there is no primary key.</param>
public sealed record TableInfo(
    string? Schema,
    string Name,
    TableKind Kind,
    IReadOnlyList<ColumnDefinition> Columns,
    IReadOnlyList<IndexInfo> Indexes,
    IReadOnlyList<ForeignKeyInfo> ForeignKeys,
    string? PrimaryKeyName = null);
