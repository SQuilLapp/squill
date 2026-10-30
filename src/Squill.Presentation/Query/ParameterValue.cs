namespace Squill.Presentation.Query;

/// <summary>A value entered by the user for a named statement parameter.</summary>
/// <param name="Name">Parameter name without its sigil.</param>
/// <param name="Text">Entered text, bound as a string.</param>
/// <param name="IsNull">Whether the parameter is bound to SQL NULL, ignoring <paramref name="Text"/>.</param>
public sealed record ParameterValue(string Name, string? Text, bool IsNull);
