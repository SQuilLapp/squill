namespace Squill.Sql.Analysis;

/// <summary>What is visible at a caret position.</summary>
/// <param name="Clause">The clause that holds the caret.</param>
/// <param name="Sources">The visible sources, those of the innermost statement first, then those of enclosing statements.</param>
public sealed record ScopeInfo(ClauseKind Clause, IReadOnlyList<VisibleSource> Sources);

/// <summary>A table, view, CTE or derived table whose columns can be referenced.</summary>
/// <param name="Schema">The schema part as written, or <see langword="null"/>.</param>
/// <param name="Name">The table or CTE name; the alias for a derived table, empty when it has none.</param>
/// <param name="Alias">The alias, or <see langword="null"/>.</param>
/// <param name="DerivedColumns">The output names of a CTE or derived table; <see langword="null"/> for a real table or when unknown (<c>*</c>).</param>
public sealed record VisibleSource(string? Schema, string Name, string? Alias, IReadOnlyList<string>? DerivedColumns);
