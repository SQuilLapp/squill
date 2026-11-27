namespace Squill.Core;

/// <summary>Builds the secret store keys of a profile.</summary>
public static class SecretKeys
{
    /// <summary>Gets the key of the database password of a profile.</summary>
    /// <param name="id">Identifier of the connection profile.</param>
    /// <returns>The secret store key.</returns>
    public static string ProfilePassword(Guid id) => $"profile:{id:N}:password";

    /// <summary>Gets the key of the SSH password or key passphrase of a profile.</summary>
    /// <param name="id">Identifier of the connection profile.</param>
    /// <returns>The secret store key.</returns>
    public static string SshSecret(Guid id) => $"profile:{id:N}:ssh";

    /// <summary>Gets the key of the passphrase of the TLS client key of a profile.</summary>
    /// <param name="id">Identifier of the connection profile.</param>
    /// <returns>The secret store key.</returns>
    public static string TlsKeyPassphrase(Guid id) => $"profile:{id:N}:tlskey";
}
