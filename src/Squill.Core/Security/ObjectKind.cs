namespace Squill.Core.Security;

/// <summary>Kind of object a privilege applies to.</summary>
public enum ObjectKind
{
    /// <summary>A table.</summary>
    Table,

    /// <summary>A view.</summary>
    View,

    /// <summary>A materialized view.</summary>
    MaterializedView,
}
