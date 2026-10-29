namespace Squill.Platform.Secrets;

/// <summary>Stores secrets (passwords, passphrases) outside the profile files, keyed by string.</summary>
public interface ISecretStore
{
    /// <summary>Stores a secret, replacing any previous value of the key.</summary>
    /// <param name="key">Secret key, see <see cref="Squill.Core.SecretKeys"/>.</param>
    /// <param name="secret">Secret value.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask SetAsync(string key, string secret, CancellationToken ct);

    /// <summary>Reads a secret.</summary>
    /// <param name="key">Secret key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The secret, or <see langword="null"/> when the key is unknown.</returns>
    ValueTask<string?> GetAsync(string key, CancellationToken ct);

    /// <summary>Deletes a secret; deleting a missing key is a no-op.</summary>
    /// <param name="key">Secret key.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask DeleteAsync(string key, CancellationToken ct);
}
