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

    /// <summary>Reads the passphrase of the TLS client key of a profile.</summary>
    /// <param name="id">Profile identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The passphrase, or <see langword="null"/> when none is stored.</returns>
    ValueTask<string?> GetTlsKeyPassphraseAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>(null);

    /// <summary>Stores the passphrase of the TLS client key of a profile; <see cref="SaveAsync"/> does not touch it.</summary>
    /// <param name="id">Profile identifier.</param>
    /// <param name="passphrase">The passphrase; <see langword="null"/> keeps the stored one and an empty string deletes it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the secret is stored.</returns>
    ValueTask SaveTlsKeyPassphraseAsync(Guid id, string? passphrase, CancellationToken ct) => ValueTask.CompletedTask;

    /// <summary>Reads the proxy password of a profile.</summary>
    /// <param name="id">Profile identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The password, or <see langword="null"/> when none is stored.</returns>
    ValueTask<string?> GetProxySecretAsync(Guid id, CancellationToken ct) => ValueTask.FromResult<string?>(null);

    /// <summary>Stores the proxy password of a profile; <see cref="SaveAsync"/> does not touch it.</summary>
    /// <param name="id">Profile identifier.</param>
    /// <param name="password">The password; <see langword="null"/> keeps the stored one and an empty string deletes it.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the secret is stored.</returns>
    ValueTask SaveProxySecretAsync(Guid id, string? password, CancellationToken ct) => ValueTask.CompletedTask;

    /// <summary>Tells whether the user already confirmed this exact password command for a profile.</summary>
    /// <param name="id">Profile identifier.</param>
    /// <param name="commandLine">The command line about to run.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the same command line was confirmed before; the default implementation never remembers.</returns>
    ValueTask<bool> IsCommandTrustedAsync(Guid id, string commandLine, CancellationToken ct) => ValueTask.FromResult(false);

    /// <summary>Remembers that the user confirmed a password command for a profile (kept in the secret store, never in the profile file, so an imported profile is not trusted).</summary>
    /// <param name="id">Profile identifier.</param>
    /// <param name="commandLine">The confirmed command line.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the confirmation is stored.</returns>
    ValueTask TrustCommandAsync(Guid id, string commandLine, CancellationToken ct) => ValueTask.CompletedTask;
}
