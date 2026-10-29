using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Squill.Platform.Storage;

/// <summary>Serializes access to a JSON list file, with atomic writes and recovery from a corrupt file.</summary>
/// <typeparam name="T">Item type of the persisted list.</typeparam>
internal sealed class JsonFileStore<T> : IDisposable
{
    private readonly string _path;
    private readonly JsonTypeInfo<List<T>> _typeInfo;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Initializes the store.</summary>
    /// <param name="path">Full path of the JSON file.</param>
    /// <param name="typeInfo">Source-generated metadata of the list.</param>
    public JsonFileStore(string path, JsonTypeInfo<List<T>> typeInfo)
    {
        _path = path;
        _typeInfo = typeInfo;
    }

    /// <summary>Releases the write lock.</summary>
    public void Dispose() => _gate.Dispose();

    /// <summary>Reads the list; a missing file is empty and a corrupt one is renamed to <c>.bad</c> and read as empty.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A new list the caller may modify.</returns>
    public async ValueTask<List<T>> ReadAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return await ReadCoreAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Reads the list, applies a change and writes it back atomically when the change reports one.</summary>
    /// <param name="mutate">Modifies the list; returns <see langword="true"/> when it must be written.</param>
    /// <param name="ct">Cancellation token.</param>
    public async ValueTask UpdateAsync(Func<List<T>, bool> mutate, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var items = await ReadCoreAsync(ct);
            if (!mutate(items))
            {
                return;
            }

            var tmp = _path + ".tmp";
            await using (var stream = File.Create(tmp))
            {
                await JsonSerializer.SerializeAsync(stream, items, _typeInfo, ct);
            }

            File.Move(tmp, _path, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async ValueTask<List<T>> ReadCoreAsync(CancellationToken ct)
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync(stream, _typeInfo, ct) ?? [];
        }
        catch (JsonException)
        {
            File.Move(_path, _path + ".bad", overwrite: true);
            return [];
        }
    }
}
