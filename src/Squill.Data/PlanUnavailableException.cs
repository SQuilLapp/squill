namespace Squill.Data;

/// <summary>The engine cannot give a plan for the requested statement or session.</summary>
/// <param name="reasonCode">Why: <c>parameters</c>, <c>permission</c>, <c>version</c>, <c>engine</c> or <c>detail</c> (the message is shown as it is).</param>
/// <param name="message">Human-readable explanation, usually the engine's own message.</param>
public sealed class PlanUnavailableException(string reasonCode, string message) : Exception(message)
{
    /// <summary>Gets the reason code: <c>parameters</c>, <c>permission</c>, <c>version</c>, <c>engine</c> or <c>detail</c> (the message is shown as it is).</summary>
    public string ReasonCode { get; } = reasonCode;
}
