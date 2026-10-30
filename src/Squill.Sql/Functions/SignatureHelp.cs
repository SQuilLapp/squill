namespace Squill.Sql.Functions;

/// <summary>The function call around the caret.</summary>
/// <param name="Signature">The signature of the function being called.</param>
/// <param name="ActiveParameter">The zero-based index of the argument holding the caret.</param>
public sealed record SignatureHelp(FunctionSignature Signature, int ActiveParameter);
