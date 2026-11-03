using Squill.Core;

namespace Squill.Sql.Analysis;

/// <summary>The description shown when the pointer rests on an object.</summary>
/// <param name="Range">The identifier the description is about.</param>
/// <param name="Markdown">The description in Markdown, in English.</param>
/// <param name="Code">Stable identifier from <see cref="SqlMessageCodes"/> that the UI maps to a translated text; null when there is none.</param>
/// <param name="Args">Values for the placeholders of the translated text, <c>Args[i]</c> filling <c>{i}</c>; for a table, the column count comes first.</param>
public sealed record HoverInfo(TextRange Range, string Markdown, string? Code = null, IReadOnlyList<string>? Args = null);
