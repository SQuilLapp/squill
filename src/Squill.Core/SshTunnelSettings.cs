namespace Squill.Core;

/// <summary>How to authenticate against the SSH server of a tunnel.</summary>
public enum SshAuthMethod
{
    Password,
    PrivateKey,
}

/// <summary>SSH tunnel settings of a profile; the password or passphrase lives in the secret store.</summary>
public sealed record SshTunnelSettings(
    string Host,
    int Port,
    string UserName,
    SshAuthMethod AuthMethod,
    string? PrivateKeyPath);
