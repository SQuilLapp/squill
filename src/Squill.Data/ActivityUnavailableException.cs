namespace Squill.Data;

/// <summary>The server activity cannot be read with the rights of the connected login.</summary>
/// <param name="messageCode">Localisable message code, for example <c>activity.permission</c>.</param>
/// <param name="message">Human-readable explanation.</param>
public sealed class ActivityUnavailableException(string messageCode, string message) : Exception(message)
{
    /// <summary>Gets the message code the UI maps to a localised text.</summary>
    public string MessageCode { get; } = messageCode;
}
