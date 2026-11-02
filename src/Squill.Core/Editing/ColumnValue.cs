namespace Squill.Core.Editing;

/// <summary>A table column and the CLR value assigned to it.</summary>
/// <param name="Column">Table column name, never an alias.</param>
/// <param name="Value">The value; SQL NULL is <see langword="null"/>.</param>
public sealed record ColumnValue(string Column, object? Value);
