using System.Text.Json;
using Squill.Core;
using Squill.Platform.Secrets;

namespace Squill.Platform.Profiles;

/// <summary>Stores profiles in <c>profiles.json</c> and their secrets in an <see cref="ISecretStore"/>.</summary>
public sealed class JsonProfileStore : IProfileStore, IDisposable
{
    private readonly string _path;
    private readonly ISecretStore _secrets;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Initializes the store.</summary>
    /// <param name="paths">Application paths; the file lives in <see cref="AppPaths.DataDirectory"/>.</param>
    /// <param name="secrets">Store holding the passwords.</param>
    public JsonProfileStore(AppPaths paths, ISecretStore secrets)
    {
        _path = paths.Combine("profiles.json");
        _secrets = secrets;
    }

    /// <summary>Releases the write lock.</summary>
    public void Dispose() => _gate.Dispose();

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ConnectionProfile>> ListAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return await ReadAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask SaveAsync(ConnectionProfile profile, string? password, string? sshSecret, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var profiles = await ReadAsync(ct);
            var index = profiles.FindIndex(p => p.Id == profile.Id);
            if (index >= 0)
            {
                profiles[index] = profile;
            }
            else
            {
                profiles.Add(profile);
            }

            await WriteAsync(profiles, ct);
            await ApplySecretAsync(SecretKeys.ProfilePassword(profile.Id), password, ct);
            await ApplySecretAsync(SecretKeys.SshSecret(profile.Id), sshSecret, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(Guid id, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var profiles = await ReadAsync(ct);
            if (profiles.RemoveAll(p => p.Id == id) > 0)
            {
                await WriteAsync(profiles, ct);
            }

            await _secrets.DeleteAsync(SecretKeys.ProfilePassword(id), ct);
            await _secrets.DeleteAsync(SecretKeys.SshSecret(id), ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public ValueTask<string?> GetPasswordAsync(Guid id, CancellationToken ct) =>
        _secrets.GetAsync(SecretKeys.ProfilePassword(id), ct);

    /// <inheritdoc/>
    public ValueTask<string?> GetSshSecretAsync(Guid id, CancellationToken ct) =>
        _secrets.GetAsync(SecretKeys.SshSecret(id), ct);

    private ValueTask ApplySecretAsync(string key, string? value, CancellationToken ct) => value switch
    {
        null => ValueTask.CompletedTask,
        "" => _secrets.DeleteAsync(key, ct),
        _ => _secrets.SetAsync(key, value, ct),
    };

    private async ValueTask<List<ConnectionProfile>> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync(stream, ProfileJsonContext.Default.ListConnectionProfile, ct) ?? [];
        }
        catch (JsonException)
        {
            File.Move(_path, _path + ".bad", overwrite: true);
            return [];
        }
    }

    private async ValueTask WriteAsync(List<ConnectionProfile> profiles, CancellationToken ct)
    {
        var tmp = _path + ".tmp";
        await using (var stream = File.Create(tmp))
        {
            await JsonSerializer.SerializeAsync(stream, profiles, ProfileJsonContext.Default.ListConnectionProfile, ct);
        }

        File.Move(tmp, _path, overwrite: true);
    }
}
