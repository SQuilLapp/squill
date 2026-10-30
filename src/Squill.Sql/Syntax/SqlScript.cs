namespace Squill.Sql.Syntax;

/// <summary>The result of parsing a text that may hold several statements.</summary>
/// <param name="Statements">One node per statement, in source order.</param>
/// <param name="Diagnostics">Every problem found, in source order of the statements.</param>
public sealed record SqlScript(IReadOnlyList<Statement> Statements, IReadOnlyList<ParseDiagnostic> Diagnostics);
