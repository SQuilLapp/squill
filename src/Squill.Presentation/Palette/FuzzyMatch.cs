namespace Squill.Presentation.Palette;

/// <summary>A successful fuzzy match of a pattern against a candidate string.</summary>
/// <param name="Score">The rank of the match, higher is better; 0 for an empty pattern.</param>
/// <param name="Ranges">The merged runs of matched characters as indexes into the original candidate, in increasing order.</param>
public sealed record FuzzyMatch(int Score, IReadOnlyList<(int Start, int Length)> Ranges);
