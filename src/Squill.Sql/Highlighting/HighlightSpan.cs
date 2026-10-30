namespace Squill.Sql.Highlighting;

/// <summary>A highlighted range of the source text.</summary>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
/// <param name="Kind">Visual category.</param>
public readonly record struct HighlightSpan(int Start, int Length, HighlightKind Kind);
