namespace Squill.Platform.Ssh;

/// <summary>An open SSH local port forward; disposing it closes the forward and the SSH connection.</summary>
public interface ISshTunnel : IAsyncDisposable
{
    /// <summary>Loopback port on which the remote endpoint is reachable.</summary>
    int LocalPort { get; }
}
