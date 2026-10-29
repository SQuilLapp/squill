namespace Squill.Core;

/// <summary>How to authenticate against the SSH server of a tunnel.</summary>
public enum SshAuthMethod
{
    /// <summary>Authenticate with the user's password.</summary>
    Password,
    /// <summary>Authenticate with a private key file, optionally protected by a passphrase.</summary>
    PrivateKey,
}

/// <summary>SSH tunnel settings of a profile; the password or passphrase lives in the secret store.</summary>
/// <param name="Host">SSH server host name or address.</param>
/// <param name="Port">SSH server port.</param>
/// <param name="UserName">Account used to sign in to the SSH server.</param>
/// <param name="AuthMethod">How the account authenticates.</param>
/// <param name="PrivateKeyPath">Path of the private key file; <see langword="null"/> when <paramref name="AuthMethod"/> is <see cref="SshAuthMethod.Password"/>.</param>
public sealed record SshTunnelSettings(
    string Host,
    int Port,
    string UserName,
    SshAuthMethod AuthMethod,
    string? PrivateKeyPath);
