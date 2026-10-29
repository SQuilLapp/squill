namespace Squill.Sql.Syntax;

/// <summary>A problem found while parsing, located in the source text.</summary>
/// <param name="Start">Zero-based offset of the first character concerned.</param>
/// <param name="Length">Number of characters concerned; zero for something missing at <paramref name="Start"/>.</param>
/// <param name="Message">Human-readable description in English.</param>
public readonly record struct ParseDiagnostic(int Start, int Length, string Message);
