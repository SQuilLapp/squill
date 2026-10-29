namespace Squill.Data;

/// <summary>A named statement parameter; a SQL NULL value is <c>null</c>.</summary>
public sealed record QueryParameter(string Name, object? Value);
