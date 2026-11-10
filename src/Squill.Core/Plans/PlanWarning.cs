namespace Squill.Core.Plans;

/// <summary>A warning attached to a plan or a node, identified by a message code.</summary>
/// <param name="Code">Stable code resolved to text by the Presentation layer.</param>
/// <param name="Args">Arguments of the message.</param>
public sealed record PlanWarning(string Code, IReadOnlyList<string> Args);
