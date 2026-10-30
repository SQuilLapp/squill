namespace Squill.Sql.Functions;

/// <summary>Describes a built-in SQL function.</summary>
/// <param name="Name">The function name as it is inserted.</param>
/// <param name="Parameters">The parameter names; a trailing <c>...</c> marks a variadic one.</param>
/// <param name="Returns">The return type, informally.</param>
/// <param name="Description">A one-line description.</param>
public sealed record FunctionSignature(string Name, IReadOnlyList<string> Parameters, string Returns, string Description)
{
    /// <summary>Gets the signature as shown to the user, for example <c>coalesce(a, b...) -> any</c>.</summary>
    public string Display => $"{Name}({string.Join(", ", Parameters)}) -> {Returns}";
}
