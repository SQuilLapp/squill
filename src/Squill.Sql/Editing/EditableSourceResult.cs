using Squill.Core.Editing;

namespace Squill.Sql.Editing;

/// <summary>Verdict of <see cref="EditableSourceAnalyzer"/>: exactly one of the two values is set.</summary>
/// <param name="Target">The table the result maps to, or <see langword="null"/> when the result is read-only.</param>
/// <param name="Reason">Why the result is read-only, shown as "Read-only: reason"; <see langword="null"/> when editable.</param>
public sealed record EditableSourceResult(EditTarget? Target, string? Reason);
