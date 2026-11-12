using System.Text.Json;

namespace Squill.Platform.Storage;

/// <summary>Stores one filter file per profile in the <c>table-filters</c> directory.</summary>
public sealed class JsonTableFilterStore : ITableFilterStore, IDisposable
{
    private readonly AppPaths _paths;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Initializes the store.</summary>
    /// <param name="paths">Application paths; files live in <c>table-filters</c> under <see cref="AppPaths.DataDirectory"/>.</param>
    public JsonTableFilterStore(AppPaths paths) => _paths = paths;

    /// <summary>Releases the write lock.</summary>
    public void Dispose() => _gate.Dispose();

    /// <inheritdoc/>
    public async ValueTask<TableFilter?> LoadAsync(Guid profileId, CancellationToken ct)
    {
        var path = PathOf(profileId);
        await _gate.WaitAsync(ct);
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            await using var stream = File.OpenRead(path);
            var filter = await JsonSerializer.DeserializeAsync(stream, StorageJsonContext.Default.TableFilter, ct);
            return filter is null
                ? null
                : new TableFilter(new Dictionary<string, IReadOnlyList<string>>(filter.Hidden, StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception e) when (e is JsonException or IOException or ArgumentException or NotSupportedException)
        {
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask SaveAsync(Guid profileId, TableFilter filter, CancellationToken ct)
    {
        var path = PathOf(profileId);
        await _gate.WaitAsync(ct);
        try
        {
            var tmp = path + ".tmp";
            await using (var stream = File.Create(tmp))
            {
                await JsonSerializer.SerializeAsync(stream, filter, StorageJsonContext.Default.TableFilter, ct);
            }

            File.Move(tmp, path, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    private string PathOf(Guid profileId) => _paths.Combine("table-filters", $"{profileId:N}.json");
}
