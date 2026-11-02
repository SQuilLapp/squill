namespace Squill.Core.Design;

/// <summary>An editable foreign key of a <see cref="TableDesign"/>.</summary>
/// <param name="OriginalName">Name in the database; null for a key added in the editor.</param>
/// <param name="Name">Current constraint name.</param>
/// <param name="Columns">Referencing column names.</param>
/// <param name="ReferencedSchema">Schema of the referenced table, if any.</param>
/// <param name="ReferencedTable">Referenced table name.</param>
/// <param name="ReferencedColumns">Referenced column names.</param>
/// <param name="OnDelete">Action taken when a referenced row is deleted.</param>
/// <param name="OnUpdate">Action taken when a referenced key is updated.</param>
public sealed record ForeignKeyDesign(
    string? OriginalName,
    string Name,
    IReadOnlyList<string> Columns,
    string? ReferencedSchema,
    string ReferencedTable,
    IReadOnlyList<string> ReferencedColumns,
    ReferentialAction OnDelete,
    ReferentialAction OnUpdate);
