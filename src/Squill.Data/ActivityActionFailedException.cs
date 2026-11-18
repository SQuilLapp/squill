namespace Squill.Data;

/// <summary>The server refused to cancel a statement or end a session (no permission, or the session is gone).</summary>
/// <param name="reasonCode">Why: <c>refused</c> (the server answered no).</param>
/// <param name="message">Human-readable explanation.</param>
public sealed class ActivityActionFailedException(string reasonCode, string message) : Exception(message)
{
    /// <summary>Gets the reason code: <c>refused</c>.</summary>
    public string ReasonCode { get; } = reasonCode;
}
