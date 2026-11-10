namespace Squill.Core.Plans;

/// <summary>Format of the raw plan text returned by the engine.</summary>
public enum PlanSourceFormat
{
    /// <summary>Plain text (rows or an indented tree).</summary>
    Text,
    /// <summary>JSON document.</summary>
    Json,
    /// <summary>XML document.</summary>
    Xml,
}
