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
}
