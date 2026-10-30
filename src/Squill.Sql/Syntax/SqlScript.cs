using Squill.Core;

namespace Squill.Sql.Syntax;

/// <summary>The result of parsing a text that may hold several statements.</summary>
/// <param name="Statements">One node per statement, in source order.</param>
/// <param name="Diagnostics">Every problem found, in source order of the statements.</param>
public sealed record SqlScript(IReadOnlyList<Statement> Statements, IReadOnlyList<ParseDiagnostic> Diagnostics)
{
    /// <summary>Gets the parsed text, which the node spans refer to.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Gets the dialect the text was parsed with.</summary>
    public DatabaseDialect Dialect { get; init; } = DatabaseDialect.PostgreSql;
}
