namespace Squill.Core.Editing;

/// <summary>A validation error on a grid row.</summary>
/// <param name="Row">Grid row index.</param>
/// <param name="Column">Table column name, or null when the issue concerns the whole row.</param>
/// <param name="Message">Human-readable description in English.</param>
/// <param name="Code">Stable identifier from <see cref="CoreMessageCodes"/> that the UI maps to a translated text; null when the message comes from the database.</param>
/// <param name="Args">Values for the placeholders of the translated text, <c>Args[i]</c> filling <c>{i}</c>.</param>
public sealed record EditIssue(long Row, string? Column, string Message, string? Code = null, IReadOnlyList<string>? Args = null);
