using Squill.Platform.Storage;

namespace Squill.Platform.Updates;

/// <summary>Stores the update state in <c>update-state.json</c>, as a single-item list so the file helper can be reused.</summary>
public sealed class JsonUpdateStateStore : IUpdateStateStore, IDisposable
{
    private readonly JsonFileStore<UpdateState> _file;

    /// <summary>Initializes the store.</summary>
    /// <param name="paths">Application paths; the file lives in <see cref="AppPaths.DataDirectory"/>.</param>
    public JsonUpdateStateStore(AppPaths paths) =>
        _file = new(paths.Combine("update-state.json"), UpdateStateJsonContext.Default.ListUpdateState);

    /// <summary>Releases the write lock.</summary>
    public void Dispose() => _file.Dispose();

    /// <inheritdoc/>
    public async ValueTask<UpdateState> LoadAsync(CancellationToken ct) =>
        (await _file.ReadAsync(ct)).FirstOrDefault() ?? new UpdateState();

    /// <inheritdoc/>
    public ValueTask SaveAsync(UpdateState state, CancellationToken ct) => _file.UpdateAsync(items =>
    {
        items.Clear();
        items.Add(state);
        return true;
    }, ct);
}
