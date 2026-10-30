using Squill.Core;

namespace Squill.Sql.Completion;

/// <summary>One proposal of a completion list.</summary>
/// <param name="Label">The text shown in the list.</param>
/// <param name="Kind">The kind of item.</param>
/// <param name="InsertText">The text that replaces <paramref name="ReplaceRange"/>.</param>
/// <param name="Detail">A short description such as a data type, or <see langword="null"/>.</param>
/// <param name="SortText">The key used to order the list, or <see langword="null"/> to sort by label.</param>
/// <param name="ReplaceRange">The prefix being typed, replaced when the item is accepted.</param>
public sealed record CompletionItem(
    string Label,
    CompletionKind Kind,
    string InsertText,
    string? Detail,
    string? SortText,
    TextRange ReplaceRange);
