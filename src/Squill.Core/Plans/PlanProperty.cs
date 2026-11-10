namespace Squill.Core.Plans;

/// <summary>A name/value pair shown in the details panel of a plan node.</summary>
/// <param name="Name">Property label as reported by the engine.</param>
/// <param name="Value">Value already formatted by the provider; numbers stay invariant strings.</param>
public sealed record PlanProperty(string Name, string Value);
