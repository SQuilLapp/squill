namespace Squill.Core;

/// <summary>A column of a table or view.</summary>
public sealed record ColumnDefinition(string Name, string DataType, bool IsNullable, bool IsPrimaryKey, string? DefaultExpression);
