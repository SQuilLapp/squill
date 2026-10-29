namespace Squill.Platform.Secrets;

/// <summary>Creates the <see cref="ISecretStore"/> native to the current operating system.</summary>
public static class SecretStoreFactory
{
    /// <summary>Creates the secret store for the current platform.</summary>
    /// <param name="dataDirectory">Application data directory; the Windows store keeps its files in its <c>secrets</c> subfolder.</param>
    /// <returns>DPAPI on Windows, Keychain on macOS, libsecret on Linux.</returns>
    /// <exception cref="PlatformNotSupportedException">The operating system has no secret store.</exception>
    public static ISecretStore Create(string dataDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            return new DpapiSecretStore(Path.Combine(dataDirectory, "secrets"));
        }

        if (OperatingSystem.IsMacOS())
        {
            return new KeychainSecretStore();
        }

        if (OperatingSystem.IsLinux())
        {
            return new LibSecretStore();
        }

        throw new PlatformNotSupportedException("No secret store is available on this operating system.");
    }
}
