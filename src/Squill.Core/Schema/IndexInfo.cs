namespace Squill.Core;

/// <summary>An index over one or more columns.</summary>
public sealed record IndexInfo(string Name, bool IsUnique, IReadOnlyList<string> Columns);
