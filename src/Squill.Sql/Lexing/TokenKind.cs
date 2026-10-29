namespace Squill.Sql.Lexing;

/// <summary>Lexical category of a <see cref="Token"/>.</summary>
public enum TokenKind
{
    /// <summary>A run of whitespace characters.</summary>
    Whitespace,
    /// <summary>A comment introduced by <c>--</c> up to the end of the line.</summary>
    LineComment,
    /// <summary>A <c>/* ... */</c> comment.</summary>
    BlockComment,
    /// <summary>A reserved word of the dialect.</summary>
    Keyword,
    /// <summary>An unquoted identifier.</summary>
    Identifier,
    /// <summary>A quoted identifier such as <c>"name"</c>.</summary>
    QuotedIdentifier,
    /// <summary>A single-quoted string literal.</summary>
    StringLiteral,
    /// <summary>An integer, decimal or exponent number.</summary>
    NumberLiteral,
    /// <summary>An operator such as <c>+</c> or <c>&lt;=</c>.</summary>
    Operator,
    /// <summary>One of <c>( ) , ; .</c>.</summary>
    Punctuation,
    /// <summary>A bind parameter or variable placeholder.</summary>
    Parameter,
    /// <summary>A character the tokenizer does not understand.</summary>
    Unknown,
    /// <summary>End of the text; consumers synthesize it, tokenizers never emit it.</summary>
    EndOfFile,
}
