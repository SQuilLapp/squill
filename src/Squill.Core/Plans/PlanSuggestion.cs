namespace Squill.Core.Plans;

/// <summary>An index suggestion reported by the engine itself (SQL Server missing indexes).</summary>
/// <param name="Title">Short heading.</param>
/// <param name="Text">The <c>CREATE INDEX</c> statement text.</param>
public sealed record PlanSuggestion(string Title, string Text);
