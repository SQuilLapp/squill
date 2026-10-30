using Squill.Core;

namespace Squill.Sql.Analysis;

/// <summary>A problem reported in the editor.</summary>
/// <param name="Range">The text concerned, always inside the analyzed text.</param>
/// <param name="Severity">How serious the problem is.</param>
/// <param name="Message">Human-readable description in English.</param>
/// <param name="Code">Stable identifier: <c>SQL0001</c> syntax, <c>SQL1001</c> unknown table, <c>SQL1002</c> unknown column.</param>
public sealed record SqlDiagnostic(TextRange Range, DiagnosticSeverity Severity, string Message, string Code);
