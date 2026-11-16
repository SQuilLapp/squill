namespace Squill.Platform.Ssh;

/// <summary>Known hosts kept in memory only (tests, and heads without a data directory).</summary>
public sealed class InMemoryKnownHostsStore : IKnownHostsStore
{
    private readonly Dictionary<string, string> _keys = [];

    /// <inheritdoc/>
    public string Location => "memory";

    /// <inheritdoc/>
    public ValueTask<string?> FindAsync(string host, int port, string algorithm, CancellationToken ct)
    {
        lock (_keys)
        {
            return ValueTask.FromResult(_keys.GetValueOrDefault(Key(host, port, algorithm)));
        }
    }

    /// <inheritdoc/>
    public ValueTask SaveAsync(SshHostKeyRequest key, CancellationToken ct)
    {
        lock (_keys)
        {
            _keys[Key(key.Host, key.Port, key.Algorithm)] = key.Fingerprint;
        }

        return ValueTask.CompletedTask;
    }

    private static string Key(string host, int port, string algorithm) => $"{host.ToLowerInvariant()}:{port}/{algorithm}";
}
