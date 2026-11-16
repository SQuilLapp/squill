using System.Globalization;
using Squill.Platform.Storage;

namespace Squill.Platform.Ssh;

/// <summary>Known hosts kept in <c>known-hosts.json</c> under <see cref="AppPaths.DataDirectory"/>.</summary>
public sealed class JsonKnownHostsStore : IKnownHostsStore, IDisposable
{
    private readonly JsonFileStore<KnownHostEntry> _file;

    /// <summary>Initializes the store.</summary>
    /// <param name="paths">Application paths.</param>
    public JsonKnownHostsStore(AppPaths paths)
    {
        Location = paths.Combine("known-hosts.json");
        _file = new JsonFileStore<KnownHostEntry>(Location, StorageJsonContext.Default.ListKnownHostEntry);
    }

    /// <inheritdoc/>
    public string Location { get; }

    /// <summary>Releases the write lock.</summary>
    public void Dispose() => _file.Dispose();

    /// <inheritdoc/>
    public async ValueTask<string?> FindAsync(string host, int port, string algorithm, CancellationToken ct)
    {
        var id = Id(host, port);
        var items = await _file.ReadAsync(ct);
        return items.Find(e => e.Host == id && e.Algorithm == algorithm)?.Fingerprint;
    }

    /// <inheritdoc/>
    public ValueTask SaveAsync(SshHostKeyRequest key, CancellationToken ct)
    {
        var id = Id(key.Host, key.Port);
        return _file.UpdateAsync(items =>
        {
            items.RemoveAll(e => e.Host == id && e.Algorithm == key.Algorithm);
            items.Add(new KnownHostEntry(id, key.Algorithm, key.Fingerprint));
            return true;
        }, ct);
    }

    private static string Id(string host, int port) => string.Create(CultureInfo.InvariantCulture, $"{host.ToLowerInvariant()}:{port}");
}

/// <summary>One remembered host key of the known hosts file.</summary>
/// <param name="Host">Lower-cased <c>host:port</c>.</param>
/// <param name="Algorithm">Host key algorithm.</param>
/// <param name="Fingerprint">SHA256 fingerprint.</param>
public sealed record KnownHostEntry(string Host, string Algorithm, string Fingerprint);
