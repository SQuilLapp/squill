namespace Squill.Core.Design;

/// <summary>An editable column of a <see cref="TableDesign"/>.</summary>
/// <param name="OriginalName">Name in the database; null for a column added in the editor (used to detect renames).</param>
/// <param name="Name">Current column name.</param>
/// <param name="DataType">Data type as written in the editor.</param>
/// <param name="IsNullable">Whether the column accepts NULL.</param>
/// <param name="DefaultExpression">Default value expression, or null.</param>
/// <param name="IsAutoIncrement">Whether the engine generates the value.</param>
public sealed record ColumnDesign(string? OriginalName, string Name, string DataType, bool IsNullable, string? DefaultExpression, bool IsAutoIncrement);
