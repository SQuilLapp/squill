namespace Squill.Core.Design;

/// <summary>An editable index of a <see cref="TableDesign"/>.</summary>
/// <param name="OriginalName">Name in the database; null for an index added in the editor.</param>
/// <param name="Name">Current index name.</param>
/// <param name="IsUnique">Whether the index enforces uniqueness.</param>
/// <param name="Columns">Current column names in index order.</param>
public sealed record IndexDesign(string? OriginalName, string Name, bool IsUnique, IReadOnlyList<string> Columns);
