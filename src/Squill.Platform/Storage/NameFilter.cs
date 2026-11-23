namespace Squill.Platform.Storage;

/// <summary>The saved visibility of one level of the explorer: names hidden one by one, and wildcard patterns that restrict what is shown.</summary>
/// <param name="Hidden">Names hidden one by one.</param>
/// <param name="Patterns">Wildcard patterns; when there is at least one, only names matching one of them are shown.</param>
public sealed record NameFilter(IReadOnlyList<string> Hidden, IReadOnlyList<string> Patterns);
