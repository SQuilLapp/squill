using System.Text.Json;

namespace Squill.Platform.Storage;

/// <summary>Stores one layout file per profile in the <c>diagrams</c> directory.</summary>
public sealed class JsonDiagramLayoutStore : IDiagramLayoutStore, IDisposable
{
    private readonly AppPaths _paths;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Initializes the store.</summary>
    /// <param name="paths">Application paths; files live in <c>diagrams</c> under <see cref="AppPaths.DataDirectory"/>.</param>
    public JsonDiagramLayoutStore(AppPaths paths) => _paths = paths;

    /// <summary>Releases the write lock.</summary>
    public void Dispose() => _gate.Dispose();

    /// <inheritdoc/>
    public async ValueTask<DiagramLayout?> LoadAsync(Guid profileId, CancellationToken ct)
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
            var layout = await JsonSerializer.DeserializeAsync(stream, StorageJsonContext.Default.DiagramLayout, ct);
            return layout is null
                ? null
                : layout with { Positions = new Dictionary<string, DiagramPosition>(layout.Positions, StringComparer.OrdinalIgnoreCase) };
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
    public async ValueTask SaveAsync(Guid profileId, DiagramLayout layout, CancellationToken ct)
    {
        var path = PathOf(profileId);
        await _gate.WaitAsync(ct);
        try
        {
            var tmp = path + ".tmp";
            await using (var stream = File.Create(tmp))
            {
                await JsonSerializer.SerializeAsync(stream, layout, StorageJsonContext.Default.DiagramLayout, ct);
            }

            File.Move(tmp, path, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(Guid profileId, CancellationToken ct)
    {
        var path = PathOf(profileId);
        await _gate.WaitAsync(ct);
        try
        {
            File.Delete(path);
        }
        finally
        {
            _gate.Release();
        }
    }

    private string PathOf(Guid profileId) => _paths.Combine("diagrams", $"{profileId:N}.json");
}
