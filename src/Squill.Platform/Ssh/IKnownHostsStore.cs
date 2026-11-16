namespace Squill.Platform.Ssh;

/// <summary>Remembers the SSH host keys the user trusted.</summary>
public interface IKnownHostsStore
{
    /// <summary>Gets where the keys are kept, for error messages.</summary>
    string Location { get; }

    /// <summary>Looks up the remembered fingerprint of a host.</summary>
    /// <param name="host">SSH server host.</param>
    /// <param name="port">SSH server port.</param>
    /// <param name="algorithm">Host key algorithm.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The fingerprint, or <see langword="null"/> when the host is unknown for that algorithm.</returns>
    ValueTask<string?> FindAsync(string host, int port, string algorithm, CancellationToken ct);

    /// <summary>Remembers a key, replacing the one of the same host and algorithm.</summary>
    /// <param name="key">The trusted key.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask SaveAsync(SshHostKeyRequest key, CancellationToken ct);
}
