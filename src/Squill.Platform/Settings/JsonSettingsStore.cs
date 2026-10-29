using Squill.Platform.Storage;

namespace Squill.Platform.Settings;

/// <summary>Stores the settings in <c>settings.json</c>, as a single-item list so the P2-06 file helper can be reused.</summary>
public sealed class JsonSettingsStore : ISettingsStore, IDisposable
{
    private readonly JsonFileStore<AppSettings> _file;

    /// <summary>Initializes the store.</summary>
    /// <param name="paths">Application paths; the file lives in <see cref="AppPaths.DataDirectory"/>.</param>
    public JsonSettingsStore(AppPaths paths) =>
        _file = new(paths.Combine("settings.json"), SettingsJsonContext.Default.ListAppSettings);

    /// <summary>Releases the write lock.</summary>
    public void Dispose() => _file.Dispose();

    /// <inheritdoc/>
    public async ValueTask<AppSettings> LoadAsync(CancellationToken ct) =>
        (await _file.ReadAsync(ct)).FirstOrDefault()?.Clamp() ?? AppSettings.Default;

    /// <inheritdoc/>
    public ValueTask SaveAsync(AppSettings settings, CancellationToken ct) => _file.UpdateAsync(items =>
    {
        items.Clear();
        items.Add(settings.Clamp());
        return true;
    }, ct);
}
