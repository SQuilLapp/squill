namespace Squill.Core.Design;

/// <summary>A dialect-independent change to one table, produced by <see cref="TableDiff"/> and translated to SQL by the DDL generators.</summary>
public abstract record SchemaOperation;

/// <summary>Renames the table.</summary>
/// <param name="From">Current table name.</param>
/// <param name="To">New table name.</param>
public sealed record RenameTable(string From, string To) : SchemaOperation;

/// <summary>Adds a column.</summary>
/// <param name="Column">The column to add.</param>
public sealed record AddColumn(ColumnDesign Column) : SchemaOperation;

/// <summary>Drops a column.</summary>
/// <param name="Name">Name of the column to drop.</param>
public sealed record DropColumn(string Name) : SchemaOperation;

/// <summary>Renames a column.</summary>
/// <param name="From">Current column name.</param>
/// <param name="To">New column name.</param>
public sealed record RenameColumn(string From, string To) : SchemaOperation;

/// <summary>Changes the type, nullability or default of a column.</summary>
/// <param name="Before">The column before the change; its name is the one after any <see cref="RenameColumn"/>.</param>
/// <param name="After">The column after the change.</param>
/// <param name="Changes">Which properties differ.</param>
public sealed record AlterColumn(ColumnDesign Before, ColumnDesign After, ColumnChange Changes) : SchemaOperation;

/// <summary>Drops the primary key.</summary>
/// <param name="Name">Constraint name, or null when the engine has none.</param>
public sealed record DropPrimaryKey(string? Name) : SchemaOperation;

/// <summary>Adds a primary key.</summary>
/// <param name="Columns">Key column names in key order.</param>
public sealed record AddPrimaryKey(IReadOnlyList<string> Columns) : SchemaOperation;

/// <summary>Creates an index.</summary>
/// <param name="Index">The index to create.</param>
public sealed record CreateIndex(IndexDesign Index) : SchemaOperation;

/// <summary>Drops an index.</summary>
/// <param name="Name">Name of the index to drop.</param>
public sealed record DropIndex(string Name) : SchemaOperation;

/// <summary>Adds a foreign key.</summary>
/// <param name="ForeignKey">The foreign key to add.</param>
public sealed record AddForeignKey(ForeignKeyDesign ForeignKey) : SchemaOperation;

/// <summary>Drops a foreign key.</summary>
/// <param name="Name">Name of the constraint to drop.</param>
public sealed record DropForeignKey(string Name) : SchemaOperation;

/// <summary>The column properties an <see cref="AlterColumn"/> changes.</summary>
[Flags]
public enum ColumnChange
{
    /// <summary>Nothing changes.</summary>
    None = 0,

    /// <summary>The data type changes.</summary>
    Type = 1,

    /// <summary>The nullability changes.</summary>
    Nullability = 2,

    /// <summary>The default expression changes.</summary>
    Default = 4,
}
