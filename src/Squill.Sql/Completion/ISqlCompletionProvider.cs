namespace Squill.Sql.Completion;

/// <summary>Proposes completions at a caret position.</summary>
public interface ISqlCompletionProvider
{
    /// <summary>Lists every candidate valid at the caret; the caller filters by prefix.</summary>
    /// <param name="request">The script, caret and dialect.</param>
    /// <returns>The candidates, possibly empty; never <see langword="null"/>.</returns>
    IReadOnlyList<CompletionItem> GetCompletions(SqlRequest request);
}
