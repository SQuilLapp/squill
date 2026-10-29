using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Squill.Platform.Secrets;

/// <summary>Windows <see cref="ISecretStore"/>: one DPAPI-protected file (current user scope) per key.</summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiSecretStore : ISecretStore
{
    private readonly string _directory;

    /// <summary>Initializes the store.</summary>
    /// <param name="directory">Directory holding the encrypted files; created on first write.</param>
    public DpapiSecretStore(string directory) => _directory = directory;

    /// <inheritdoc/>
    public async ValueTask SetAsync(string key, string secret, CancellationToken ct)
    {
        byte[] protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), null, DataProtectionScope.CurrentUser);
        Directory.CreateDirectory(_directory);
        await File.WriteAllBytesAsync(PathOf(key), protectedBytes, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<string?> GetAsync(string key, CancellationToken ct)
    {
        string path = PathOf(key);
        if (!File.Exists(path))
        {
            return null;
        }

        byte[] protectedBytes = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
        return Encoding.UTF8.GetString(ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser));
    }

    /// <inheritdoc/>
    public ValueTask DeleteAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        File.Delete(PathOf(key));
        return ValueTask.CompletedTask;
    }

    private string PathOf(string key) =>
        Path.Combine(_directory, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".bin");
}
