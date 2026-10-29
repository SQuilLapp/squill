namespace Squill.Core;

/// <summary>A foreign key constraint.</summary>
public sealed record ForeignKeyInfo(
    string Name,
    IReadOnlyList<string> Columns,
    string? ReferencedSchema,
    string ReferencedTable,
    IReadOnlyList<string> ReferencedColumns);
