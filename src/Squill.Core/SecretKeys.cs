namespace Squill.Core;

/// <summary>Builds the secret store keys of a profile.</summary>
public static class SecretKeys
{
    public static string ProfilePassword(Guid id) => $"profile:{id:N}:password";

    public static string SshSecret(Guid id) => $"profile:{id:N}:ssh";
}
