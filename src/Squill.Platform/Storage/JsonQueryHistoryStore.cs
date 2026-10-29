namespace Squill.Platform.Storage;

/// <summary>Stores the query history in <c>history.json</c>.</summary>
public sealed class JsonQueryHistoryStore : IQueryHistoryStore, IDisposable
{
    /// <summary>Maximum number of entries kept.</summary>
    public const int Capacity = 5000;

    private readonly JsonFileStore<HistoryEntry> _file;

    /// <summary>Initializes the store.</summary>
    /// <param name="paths">Application paths; the file lives in <see cref="AppPaths.DataDirectory"/>.</param>
    public JsonQueryHistoryStore(AppPaths paths) =>
        _file = new(paths.Combine("history.json"), StorageJsonContext.Default.ListHistoryEntry);

    /// <summary>Releases the write lock.</summary>
    public void Dispose() => _file.Dispose();

    /// <inheritdoc/>
    public ValueTask AppendAsync(HistoryEntry e, CancellationToken ct) => _file.UpdateAsync(items =>
    {
        items.Add(e);
        if (items.Count > Capacity)
        {
            items.RemoveRange(0, items.Count - Capacity);
        }

        return true;
    }, ct);

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<HistoryEntry>> ListAsync(int max, CancellationToken ct)
    {
        var items = await _file.ReadAsync(ct);
        var take = Math.Clamp(max, 0, items.Count);
        return items.GetRange(items.Count - take, take);
    }

    /// <inheritdoc/>
    public ValueTask ClearAsync(CancellationToken ct) => _file.UpdateAsync(items =>
    {
        items.Clear();
        return true;
    }, ct);
}
