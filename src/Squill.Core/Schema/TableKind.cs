namespace Squill.Core;

/// <summary>Kind of a relation in a schema snapshot.</summary>
public enum TableKind
{
    /// <summary>A base table that stores rows.</summary>
    Table,

    /// <summary>A view defined by a query.</summary>
    View,

    /// <summary>A view whose result is stored and refreshed on demand (PostgreSQL only); read-only, excluded from the ER diagram like a view.</summary>
    MaterializedView,
}

/// <summary>Helpers on <see cref="TableKind"/>.</summary>
public static class TableKindExtensions
{
    /// <summary>Whether the relation is defined by a query (a view or a materialized view): not editable, not drawn in the ER diagram.</summary>
    /// <param name="kind">The kind to test.</param>
    /// <returns><see langword="true"/> for <see cref="TableKind.View"/> and <see cref="TableKind.MaterializedView"/>.</returns>
    public static bool IsViewLike(this TableKind kind) => kind is TableKind.View or TableKind.MaterializedView;
}
