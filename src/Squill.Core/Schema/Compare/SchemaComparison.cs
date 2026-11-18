namespace Squill.Core.Schema.Compare;

/// <summary>One thing that differs inside a modified object.</summary>
/// <param name="Kind">What changed.</param>
/// <param name="Subject">Name of the column, key or index concerned; empty for a definition or the primary key.</param>
/// <param name="SourceText">Description on the source side; null when absent there or unreadable.</param>
/// <param name="TargetText">Description on the target side; null when absent there or unreadable.</param>
public sealed record DifferenceDetail(DetailKind Kind, string Subject, string? SourceText, string? TargetText);

/// <summary>An object that differs between two snapshots.</summary>
/// <param name="Category">Group of the object.</param>
/// <param name="Schema">Schema of the object (of the table for an index); null when the engine has none.</param>
/// <param name="Name">Name of the object; <c>table.index</c> for an index and the signature for a routine, so that overloads stay distinct.</param>
/// <param name="Kind">Added, removed or modified.</param>
/// <param name="Details">What changed; empty unless <paramref name="Kind"/> is <see cref="DifferenceKind.Modified"/>.</param>
public sealed record ObjectDifference(ObjectCategory Category, string? Schema, string Name, DifferenceKind Kind, IReadOnlyList<DifferenceDetail> Details);

/// <summary>The result of <see cref="SchemaComparer.Compare"/>.</summary>
/// <param name="Differences">Differences ordered by category, schema, name.</param>
public sealed record SchemaComparison(IReadOnlyList<ObjectDifference> Differences)
{
    /// <summary>Number of differences of the given kind.</summary>
    /// <param name="kind">The kind to count.</param>
    /// <returns>The count.</returns>
    public int Count(DifferenceKind kind) => Differences.Count(d => d.Kind == kind);
}
