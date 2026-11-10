namespace Squill.Data;

/// <summary>The engine cannot give a plan for the requested statement or session.</summary>
/// <param name="reasonCode">Why: <c>parameters</c>, <c>permission</c>, <c>version</c> or <c>engine</c>.</param>
/// <param name="message">Human-readable explanation, usually the engine's own message.</param>
public sealed class PlanUnavailableException(string reasonCode, string message) : Exception(message)
{
    /// <summary>Gets the reason code: <c>parameters</c>, <c>permission</c>, <c>version</c> or <c>engine</c>.</summary>
    public string ReasonCode { get; } = reasonCode;
}
