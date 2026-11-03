namespace Squill.Sql.Syntax;

/// <summary>A problem found while parsing, located in the source text.</summary>
/// <param name="Start">Zero-based offset of the first character concerned.</param>
/// <param name="Length">Number of characters concerned; zero for something missing at <paramref name="Start"/>.</param>
/// <param name="Message">Human-readable description in English.</param>
/// <param name="Code">Stable identifier from <see cref="SqlMessageCodes"/> that the UI maps to a translated text.</param>
/// <param name="Args">Values for the placeholders of the translated text, <c>Args[i]</c> filling <c>{i}</c>.</param>
public readonly record struct ParseDiagnostic(int Start, int Length, string Message, string? Code = null, IReadOnlyList<string>? Args = null);
