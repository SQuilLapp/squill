namespace Squill.Data.Oracle;

/// <summary>An error reported by the Oracle bridge process (driver error, bad request, or a bridge that died).</summary>
public sealed class OracleBridgeException : Exception
{
    /// <summary>Creates the exception with the message the bridge reported.</summary>
    /// <param name="message">The driver or bridge error text.</param>
    public OracleBridgeException(string message) : base(message)
    {
    }

    /// <summary>Creates the exception with the message and the failure that caused it.</summary>
    /// <param name="message">What failed.</param>
    /// <param name="innerException">The underlying I/O or process failure.</param>
    public OracleBridgeException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
