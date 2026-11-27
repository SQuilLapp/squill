using System.Text;

namespace Squill.Data.Informix;

/// <summary>An ODBC call failed; the message joins every diagnostic record the driver returned.</summary>
internal sealed class OdbcException : Exception
{
    /// <summary>Initializes the exception.</summary>
    /// <param name="message">All diagnostic records, one per line.</param>
    /// <param name="sqlState">The SQLSTATE of the first record, empty when the driver gave none.</param>
    /// <param name="nativeError">The native error code of the first record.</param>
    public OdbcException(string message, string sqlState, int nativeError) : base(message)
    {
        SqlState = sqlState;
        NativeError = nativeError;
    }

    /// <summary>Gets the SQLSTATE of the first diagnostic record.</summary>
    public string SqlState { get; }

    /// <summary>Gets the native (driver or server) error code of the first diagnostic record.</summary>
    public int NativeError { get; }

    /// <summary>Reads the diagnostic records of a handle and builds the exception for a failed call.</summary>
    /// <param name="api">The ODBC entry points.</param>
    /// <param name="returnCode">The failed return code.</param>
    /// <param name="handleType">The type of <paramref name="handle"/>.</param>
    /// <param name="handle">The handle the call used; 0 when it has none.</param>
    /// <returns>The exception, never <see langword="null"/>.</returns>
    public static OdbcException From(IOdbcApi api, short returnCode, short handleType, nint handle)
    {
        var text = new StringBuilder();
        var firstState = "";
        var firstNative = 0;
        for (short record = 1; handle != 0 && api.GetDiagRec(handleType, handle, record, out var state, out var native, out var message); record++)
        {
            if (record == 1)
                (firstState, firstNative) = (state, native);
            text.Append('[').Append(state).Append("] ").Append(message).Append(" (").Append(native).Append(")\n");
        }

        if (text.Length == 0)
            text.Append("ODBC call failed with return code ").Append(returnCode);
        return new OdbcException(text.ToString().TrimEnd('\n'), firstState, firstNative);
    }
}
