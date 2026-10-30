namespace Squill.Sql.Lexing;

/// <summary>A lexical token: a kind and a span of the source text.</summary>
/// <param name="Kind">Lexical category.</param>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public readonly record struct Token(TokenKind Kind, int Start, int Length)
{
    /// <summary>Gets the offset just after the last character.</summary>
    public int End => Start + Length;

    /// <summary>Extracts the token text from the source it was produced from.</summary>
    /// <param name="source">The tokenized text.</param>
    /// <returns>The characters covered by this token.</returns>
    public string Text(string source) => source.Substring(Start, Length);
}
