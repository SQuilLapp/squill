namespace Squill.Platform.Storage;

/// <summary>Stores snippets in <c>snippets.json</c>.</summary>
public sealed class JsonSnippetStore : ISnippetStore, IDisposable
{
    private readonly JsonFileStore<Snippet> _file;

    /// <summary>Initializes the store.</summary>
    /// <param name="paths">Application paths; the file lives in <see cref="AppPaths.DataDirectory"/>.</param>
    public JsonSnippetStore(AppPaths paths) =>
        _file = new(paths.Combine("snippets.json"), StorageJsonContext.Default.ListSnippet);

    /// <summary>Releases the write lock.</summary>
    public void Dispose() => _file.Dispose();

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<Snippet>> ListAsync(CancellationToken ct) => await _file.ReadAsync(ct);

    /// <inheritdoc/>
    public ValueTask SaveAsync(Snippet snippet, CancellationToken ct) => _file.UpdateAsync(items =>
    {
        var index = items.FindIndex(s => s.Id == snippet.Id);
        if (index >= 0)
        {
            items[index] = snippet;
        }
        else
        {
            items.Add(snippet);
        }

        return true;
    }, ct);

    /// <inheritdoc/>
    public ValueTask DeleteAsync(Guid id, CancellationToken ct) =>
        _file.UpdateAsync(items => items.RemoveAll(s => s.Id == id) > 0, ct);
}
