using System.Collections.Concurrent;

namespace Squill.Platform.Secrets;

/// <summary>Non-persistent <see cref="ISecretStore"/>, used by tests and as a fallback.</summary>
public sealed class InMemorySecretStore : ISecretStore
{
    private readonly ConcurrentDictionary<string, string> _secrets = new();

    /// <inheritdoc/>
    public ValueTask SetAsync(string key, string secret, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _secrets[key] = secret;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<string?> GetAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_secrets.GetValueOrDefault(key));
    }

    /// <inheritdoc/>
    public ValueTask DeleteAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _secrets.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }
}
