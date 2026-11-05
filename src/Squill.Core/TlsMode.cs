namespace Squill.Core;

/// <summary>How a connection to a database server is secured.</summary>
public enum TlsMode
{
    /// <summary>The driver's own default; keeps the behavior of profiles saved before this option existed.</summary>
    Default,

    /// <summary>No encryption, where the engine allows it.</summary>
    Disable,

    /// <summary>Encrypt the connection without insisting on a valid certificate chain.</summary>
    Require,

    /// <summary>Encrypt the connection and verify the certificate chain and the host name.</summary>
    VerifyFull,
}
