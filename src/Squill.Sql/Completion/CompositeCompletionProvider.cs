namespace Squill.Sql.Completion;

/// <summary>Merges the proposals of several providers into one list ordered by <see cref="CompletionItem.SortText"/>.</summary>
/// <param name="providers">The providers to query.</param>
public sealed class CompositeCompletionProvider(params ISqlCompletionProvider[] providers) : ISqlCompletionProvider
{
    /// <inheritdoc />
    public IReadOnlyList<CompletionItem> GetCompletions(SqlRequest request) =>
        [.. providers
            .SelectMany(p => p.GetCompletions(request))
            .OrderBy(i => i.SortText ?? "2" + i.Label, StringComparer.Ordinal)];
}
