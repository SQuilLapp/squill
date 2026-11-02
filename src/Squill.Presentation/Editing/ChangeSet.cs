using Squill.Core.Editing;

namespace Squill.Presentation.Editing;

/// <summary>A row edit together with the grid row it comes from.</summary>
/// <param name="Row">Grid row index at the time the change set was built.</param>
/// <param name="Edit">The edit to execute.</param>
public sealed record PendingEdit(long Row, RowEdit Edit);

/// <summary>Ordered edits of a grid (deletes, updates, then inserts) and the validation errors found.</summary>
/// <param name="Edits">Edits in execution order.</param>
/// <param name="Issues">Validation errors; the change set must not be committed while any exists.</param>
public sealed record ChangeSet(IReadOnlyList<PendingEdit> Edits, IReadOnlyList<EditIssue> Issues);
