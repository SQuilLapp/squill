namespace Squill.Sql.Editing;

/// <summary>A named parameter of a generated statement, referenced as <c>@name</c> in the SQL.</summary>
/// <param name="Name">Parameter name without the leading <c>@</c>.</param>
/// <param name="Value">The value to bind; SQL NULL is <see langword="null"/>.</param>
public sealed record DmlParameter(string Name, object? Value);

/// <summary>A generated INSERT, UPDATE or DELETE statement with its parameters.</summary>
/// <param name="Sql">The statement text.</param>
/// <param name="Parameters">The parameters in order of appearance.</param>
public sealed record DmlStatement(string Sql, IReadOnlyList<DmlParameter> Parameters);
