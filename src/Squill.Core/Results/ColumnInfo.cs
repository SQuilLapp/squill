namespace Squill.Core;

/// <summary>Describes one column of a result set; row values are <c>object?[]</c> with SQL NULL as <c>null</c>.</summary>
public sealed record ColumnInfo(string Name, string DataTypeName, Type ClrType, bool IsNullable, int Ordinal);
