namespace Squill.Platform.Ssh;

/// <summary>A host key presented by an SSH server.</summary>
/// <param name="Host">SSH server host name as the user typed it.</param>
/// <param name="Port">SSH server port.</param>
/// <param name="Algorithm">Host key algorithm, for example <c>ssh-ed25519</c>.</param>
/// <param name="Fingerprint">OpenSSH style fingerprint: <c>SHA256:</c> followed by the unpadded base64 SHA-256 of the key blob.</param>
public sealed record SshHostKeyRequest(string Host, int Port, string Algorithm, string Fingerprint);
