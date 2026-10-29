using Squill.Core;
using Squill.Platform.Secrets;
using Squill.Platform.Storage;

namespace Squill.Platform.Profiles;

/// <summary>Stores profiles in <c>profiles.json</c> and their secrets in an <see cref="ISecretStore"/>.</summary>
public sealed class JsonProfileStore : IProfileStore, IDisposable
{
    private readonly JsonFileStore<ConnectionProfile> _file;
    private readonly ISecretStore _secrets;

    /// <summary>Initializes the store.</summary>
    /// <param name="paths">Application paths; the file lives in <see cref="AppPaths.DataDirectory"/>.</param>
    /// <param name="secrets">Store holding the passwords.</param>
    public JsonProfileStore(AppPaths paths, ISecretStore secrets)
    {
        _file = new(paths.Combine("profiles.json"), ProfileJsonContext.Default.ListConnectionProfile);
        _secrets = secrets;
    }

    /// <summary>Releases the write lock.</summary>
    public void Dispose() => _file.Dispose();

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<ConnectionProfile>> ListAsync(CancellationToken ct) => await _file.ReadAsync(ct);

    /// <inheritdoc/>
    public async ValueTask SaveAsync(ConnectionProfile profile, string? password, string? sshSecret, CancellationToken ct)
    {
        await _file.UpdateAsync(profiles =>
        {
            var index = profiles.FindIndex(p => p.Id == profile.Id);
            if (index >= 0)
            {
                profiles[index] = profile;
            }
            else
            {
                profiles.Add(profile);
            }

            return true;
        }, ct);
        await ApplySecretAsync(SecretKeys.ProfilePassword(profile.Id), password, ct);
        await ApplySecretAsync(SecretKeys.SshSecret(profile.Id), sshSecret, ct);
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(Guid id, CancellationToken ct)
    {
        await _file.UpdateAsync(profiles => profiles.RemoveAll(p => p.Id == id) > 0, ct);
        await _secrets.DeleteAsync(SecretKeys.ProfilePassword(id), ct);
        await _secrets.DeleteAsync(SecretKeys.SshSecret(id), ct);
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
}
