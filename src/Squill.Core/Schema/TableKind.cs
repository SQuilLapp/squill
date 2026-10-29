namespace Squill.Core;

/// <summary>Kind of a relation in a schema snapshot.</summary>
public enum TableKind
{
    /// <summary>A base table that stores rows.</summary>
    Table,

    /// <summary>A view defined by a query.</summary>
    View,
}
