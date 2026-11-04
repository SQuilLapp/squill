namespace Squill.Core;

/// <summary>A column of a table or view.</summary>
/// <param name="Name">Column name.</param>
/// <param name="DataType">Data type as the engine reports it.</param>
/// <param name="IsNullable">Whether the column accepts NULL.</param>
/// <param name="IsPrimaryKey">Whether the column is part of the primary key.</param>
/// <param name="DefaultExpression">Default value expression; ignored by consumers when <paramref name="IsAutoIncrement"/> is true.</param>
/// <param name="IsAutoIncrement">Whether the engine generates the value (identity, serial, AUTO_INCREMENT, rowid alias).</param>
/// <param name="IsGenerated">Whether the engine computes the value and refuses writes (generated or computed column, rowversion, identity <c>ALWAYS</c>).</param>
public sealed record ColumnDefinition(string Name, string DataType, bool IsNullable, bool IsPrimaryKey, string? DefaultExpression, bool IsAutoIncrement = false, bool IsGenerated = false);
