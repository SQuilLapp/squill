namespace Squill.Core.Editing;

/// <summary>A result column that maps one-to-one to a table column.</summary>
/// <param name="ResultOrdinal">The <c>ColumnInfo.Ordinal</c> of the result column.</param>
/// <param name="Definition">The table column it maps to.</param>
public sealed record EditColumn(int ResultOrdinal, ColumnDefinition Definition);
