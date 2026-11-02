namespace Squill.Core.Editing;

/// <summary>The table a result grid edits.</summary>
/// <param name="Schema">Schema name, if the engine has one.</param>
/// <param name="Table">Table name.</param>
/// <param name="Columns">Only the result columns that map to table columns; others (for example computed ones) are read-only.</param>
/// <param name="KeyColumns">Primary-key column names in key order; each one is in <paramref name="Columns"/>.</param>
/// <param name="Info">Schema snapshot entry, used by generators for column types and auto-increment flags.</param>
public sealed record EditTarget(
    string? Schema,
    string Table,
    IReadOnlyList<EditColumn> Columns,
    IReadOnlyList<string> KeyColumns,
    TableInfo Info)
{
    /// <summary>Finds the mapped column of a result ordinal.</summary>
    /// <returns>The column, or null when the ordinal is not mapped to a table column.</returns>
    public EditColumn? ByOrdinal(int resultOrdinal) => Columns.FirstOrDefault(c => c.ResultOrdinal == resultOrdinal);

    /// <summary>Whether the result column at the ordinal maps to a table column.</summary>
    public bool IsEditable(int resultOrdinal) => ByOrdinal(resultOrdinal) is not null;
}
