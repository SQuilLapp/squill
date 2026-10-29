namespace Squill.Core;

/// <summary>A table or view with its columns, indexes and foreign keys.</summary>
public sealed record TableInfo(
    string? Schema,
    string Name,
    TableKind Kind,
    IReadOnlyList<ColumnDefinition> Columns,
    IReadOnlyList<IndexInfo> Indexes,
    IReadOnlyList<ForeignKeyInfo> ForeignKeys);
