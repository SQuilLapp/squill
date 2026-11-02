namespace Squill.Core.Editing;

/// <summary>A validation error on a grid row.</summary>
/// <param name="Row">Grid row index.</param>
/// <param name="Column">Table column name, or null when the issue concerns the whole row.</param>
/// <param name="Message">Human-readable description.</param>
public sealed record EditIssue(long Row, string? Column, string Message);
