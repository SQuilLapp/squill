namespace Squill.Core.Schema.Compare;

/// <summary>How an object differs between the source and the target snapshot.</summary>
public enum DifferenceKind
{
    /// <summary>In the source only: a script would create it on the target.</summary>
    Added,

    /// <summary>In the target only: a script would drop it.</summary>
    Removed,

    /// <summary>In both, with different content.</summary>
    Modified,
}

/// <summary>The group an object belongs to in the comparison list.</summary>
public enum ObjectCategory
{
    /// <summary>A base table.</summary>
    Table,

    /// <summary>A view or materialized view.</summary>
    View,

    /// <summary>An index of a table present on both sides.</summary>
    Index,

    /// <summary>A stored function or procedure.</summary>
    Routine,
}

/// <summary>What changed inside a modified object.</summary>
public enum DetailKind
{
    /// <summary>The column exists in the source only.</summary>
    ColumnAdded,

    /// <summary>The column exists in the target only.</summary>
    ColumnRemoved,

    /// <summary>The column's type, nullability, default or identity differs.</summary>
    ColumnChanged,

    /// <summary>The primary-key columns differ.</summary>
    PrimaryKeyChanged,

    /// <summary>The foreign key exists in the source only.</summary>
    ForeignKeyAdded,

    /// <summary>The foreign key exists in the target only.</summary>
    ForeignKeyRemoved,

    /// <summary>The foreign key's columns, reference or actions differ.</summary>
    ForeignKeyChanged,

    /// <summary>The index exists in the source only.</summary>
    IndexAdded,

    /// <summary>The index exists in the target only.</summary>
    IndexRemoved,

    /// <summary>The index's uniqueness or columns differ.</summary>
    IndexChanged,

    /// <summary>The view or routine definition differs.</summary>
    DefinitionChanged,

    /// <summary>One side has no readable definition, so the real difference is unknown.</summary>
    DefinitionUnknown,
}
