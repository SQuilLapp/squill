using Squill.Core;

namespace Squill.Platform.Profiles;

/// <summary>Persists connection profiles; their secrets live in an <see cref="Secrets.ISecretStore"/>.</summary>
public interface IProfileStore
{
    /// <summary>Lists every saved profile.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The profiles in saving order.</returns>
    ValueTask<IReadOnlyList<ConnectionProfile>> ListAsync(CancellationToken ct);

    /// <summary>Inserts or replaces a profile and updates its secrets.</summary>
    /// <param name="profile">Profile to save, matched by <see cref="ConnectionProfile.Id"/>.</param>
    /// <param name="password">Database password; <see langword="null"/> keeps the stored one, empty deletes it.</param>
    /// <param name="sshSecret">SSH password or passphrase; <see langword="null"/> keeps the stored one, empty deletes it.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask SaveAsync(ConnectionProfile profile, string? password, string? sshSecret, CancellationToken ct);

    /// <summary>Deletes a profile and both of its secrets; an unknown id is a no-op.</summary>
    /// <param name="id">Profile identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask DeleteAsync(Guid id, CancellationToken ct);

    /// <summary>Reads the database password of a profile.</summary>
    /// <param name="id">Profile identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The password, or <see langword="null"/> when none is stored.</returns>
    ValueTask<string?> GetPasswordAsync(Guid id, CancellationToken ct);

    /// <summary>Reads the SSH password or key passphrase of a profile.</summary>
    /// <param name="id">Profile identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The secret, or <see langword="null"/> when none is stored.</returns>
    ValueTask<string?> GetSshSecretAsync(Guid id, CancellationToken ct);
}
