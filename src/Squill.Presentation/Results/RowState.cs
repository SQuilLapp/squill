namespace Squill.Presentation.Results;

/// <summary>Edit state of a grid row in an <see cref="EditableGridDataSource"/>.</summary>
public enum RowState
{
    /// <summary>The row matches the database.</summary>
    Unchanged,

    /// <summary>At least one cell has a pending change.</summary>
    Modified,

    /// <summary>The row was added and not yet committed.</summary>
    Inserted,

    /// <summary>The row is marked for deletion and still visible.</summary>
    Deleted,
}
