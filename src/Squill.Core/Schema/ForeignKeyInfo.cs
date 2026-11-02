namespace Squill.Core;

/// <summary>A foreign key constraint.</summary>
/// <param name="Name">Constraint name.</param>
/// <param name="Columns">Referencing columns.</param>
/// <param name="ReferencedSchema">Schema of the referenced table, if any.</param>
/// <param name="ReferencedTable">Referenced table name.</param>
/// <param name="ReferencedColumns">Referenced columns.</param>
/// <param name="OnDelete">Action taken when a referenced row is deleted.</param>
/// <param name="OnUpdate">Action taken when a referenced key is updated.</param>
public sealed record ForeignKeyInfo(
    string Name,
    IReadOnlyList<string> Columns,
    string? ReferencedSchema,
    string ReferencedTable,
    IReadOnlyList<string> ReferencedColumns,
    ReferentialAction OnDelete = ReferentialAction.NoAction,
    ReferentialAction OnUpdate = ReferentialAction.NoAction);
