namespace Squill.Platform.Ssh;

/// <summary>Asks the user whether an unknown SSH host key can be trusted.</summary>
/// <param name="request">The key the server presented.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns><see langword="true"/> to trust and remember the key.</returns>
public delegate ValueTask<bool> SshHostKeyPrompt(SshHostKeyRequest request, CancellationToken ct);
