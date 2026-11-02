namespace Squill.Core.Editing;

/// <summary>A row edit: the unit a commit is made of.</summary>
public abstract record RowEdit;

/// <summary>Inserts a row.</summary>
/// <param name="Values">Only the columns the user filled or that have no default; omitted columns take the database default.</param>
public sealed record InsertRow(IReadOnlyList<ColumnValue> Values) : RowEdit;

/// <summary>Updates a row identified by its key.</summary>
/// <param name="Key">Original values of the key columns, in key order.</param>
/// <param name="Changes">Only the modified columns with their new values; key columns may appear here.</param>
public sealed record UpdateRow(IReadOnlyList<ColumnValue> Key, IReadOnlyList<ColumnValue> Changes) : RowEdit;

/// <summary>Deletes a row identified by its key.</summary>
/// <param name="Key">Original values of the key columns, in key order.</param>
public sealed record DeleteRow(IReadOnlyList<ColumnValue> Key) : RowEdit;
