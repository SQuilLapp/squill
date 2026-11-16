namespace Squill.Platform.Ssh;

/// <summary>The SSH server's host key was not accepted.</summary>
public abstract class SshHostKeyException : Exception
{
    /// <summary>Initializes the exception.</summary>
    /// <param name="request">The key the server presented.</param>
    /// <param name="message">What went wrong.</param>
    protected SshHostKeyException(SshHostKeyRequest request, string message) : base(message) => Request = request;

    /// <summary>Gets the key the server presented.</summary>
    public SshHostKeyRequest Request { get; }
}

/// <summary>The host key is not the one remembered for that host: possible man in the middle.</summary>
public sealed class SshHostKeyChangedException : SshHostKeyException
{
    /// <summary>Initializes the exception.</summary>
    /// <param name="request">The key the server presented.</param>
    /// <param name="location">Where the remembered keys are kept.</param>
    public SshHostKeyChangedException(SshHostKeyRequest request, string location)
        : base(request, $"The host key of {request.Host}:{request.Port} changed ({request.Algorithm} {request.Fingerprint}). Remembered keys are in {location}.") =>
        Location = location;

    /// <summary>Gets where the remembered keys are kept.</summary>
    public string Location { get; }
}

/// <summary>The host key is unknown and was not trusted.</summary>
public sealed class SshHostKeyRejectedException : SshHostKeyException
{
    /// <summary>Initializes the exception.</summary>
    /// <param name="request">The key the server presented.</param>
    public SshHostKeyRejectedException(SshHostKeyRequest request)
        : base(request, $"The host key of {request.Host}:{request.Port} ({request.Algorithm} {request.Fingerprint}) was not trusted.")
    {
    }
}
