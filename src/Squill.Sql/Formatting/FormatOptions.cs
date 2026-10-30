using Squill.Core;

namespace Squill.Sql.Formatting;

/// <summary>Options of <see cref="SqlFormatter"/>.</summary>
/// <param name="KeywordCase">How keyword tokens are cased.</param>
/// <param name="IndentSize">Number of spaces per indentation level; must not be negative.</param>
public readonly record struct FormatOptions(KeywordCase KeywordCase, int IndentSize)
{
    /// <summary>Gets the default options: upper-case keywords and four-space indentation.</summary>
    public static FormatOptions Default => new(KeywordCase.Upper, 4);
}
