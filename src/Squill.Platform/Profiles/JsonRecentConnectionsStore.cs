using Squill.Platform.Storage;

namespace Squill.Platform.Profiles;

/// <summary>Stores the recent connections in <c>recent.json</c>.</summary>
public sealed class JsonRecentConnectionsStore : IRecentConnectionsStore, IDisposable
{
    private const int MaxEntries = 20;

    private readonly JsonFileStore<RecentConnection> _file;

    /// <summary>Initializes the store.</summary>
    /// <param name="paths">Application paths; the file lives in <see cref="AppPaths.DataDirectory"/>.</param>
    public JsonRecentConnectionsStore(AppPaths paths) =>
        _file = new(paths.Combine("recent.json"), StorageJsonContext.Default.ListRecentConnection);

    /// <summary>Releases the write lock.</summary>
    public void Dispose() => _file.Dispose();

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RecentConnection>> ListAsync(CancellationToken ct) =>
        [.. (await _file.ReadAsync(ct)).OrderByDescending(r => r.LastOpened).Take(MaxEntries)];

    /// <inheritdoc/>
    public async Task TouchAsync(Guid profileId, DateTimeOffset openedAt, CancellationToken ct) =>
        await _file.UpdateAsync(items =>
        {
            items.RemoveAll(r => r.ProfileId == profileId);
            items.Add(new(profileId, openedAt));
            items.Sort((a, b) => b.LastOpened.CompareTo(a.LastOpened));
            if (items.Count > MaxEntries)
            {
                items.RemoveRange(MaxEntries, items.Count - MaxEntries);
            }

            return true;
        }, ct);

    /// <inheritdoc/>
    public async Task ForgetAsync(Guid profileId, CancellationToken ct) =>
        await _file.UpdateAsync(items => items.RemoveAll(r => r.ProfileId == profileId) > 0, ct);
}
