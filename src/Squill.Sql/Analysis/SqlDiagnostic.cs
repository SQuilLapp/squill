using Squill.Core;

namespace Squill.Sql.Analysis;

/// <summary>A problem reported in the editor.</summary>
/// <param name="Range">The text concerned, always inside the analyzed text.</param>
/// <param name="Severity">How serious the problem is.</param>
/// <param name="Message">Human-readable description in English.</param>
/// <param name="Code">Stable identifier: <c>SQL0001</c> syntax, <c>SQL1001</c> unknown table, <c>SQL1002</c> unknown column.</param>
/// <param name="MessageCode">Stable identifier from <see cref="SqlMessageCodes"/> that the UI maps to a translated text; null when there is none.</param>
/// <param name="Args">Values for the placeholders of the translated text, <c>Args[i]</c> filling <c>{i}</c>.</param>
public sealed record SqlDiagnostic(
    TextRange Range, DiagnosticSeverity Severity, string Message, string Code, string? MessageCode = null, IReadOnlyList<string>? Args = null);
