namespace Squill.Platform.Storage;

/// <summary>Persists named SQL snippets.</summary>
public interface ISnippetStore
{
    /// <summary>Lists every snippet.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The snippets in saving order.</returns>
    ValueTask<IReadOnlyList<Snippet>> ListAsync(CancellationToken ct);

    /// <summary>Inserts or replaces a snippet.</summary>
    /// <param name="snippet">Snippet to save, matched by <see cref="Snippet.Id"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask SaveAsync(Snippet snippet, CancellationToken ct);

    /// <summary>Deletes a snippet; an unknown id is a no-op.</summary>
    /// <param name="id">Snippet identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask DeleteAsync(Guid id, CancellationToken ct);
}
