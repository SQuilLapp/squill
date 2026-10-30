namespace Squill.Sql.Highlighting;

/// <summary>Visual category of a <see cref="HighlightSpan"/>.</summary>
public enum HighlightKind
{
    /// <summary>A reserved word.</summary>
    Keyword,
    /// <summary>An unquoted identifier.</summary>
    Identifier,
    /// <summary>A quoted identifier.</summary>
    QuotedIdentifier,
    /// <summary>A string literal.</summary>
    StringLiteral,
    /// <summary>A numeric literal.</summary>
    Number,
    /// <summary>A line or block comment.</summary>
    Comment,
    /// <summary>An operator.</summary>
    Operator,
    /// <summary>A bind parameter or variable.</summary>
    Parameter,
    /// <summary>A built-in function name followed by an opening parenthesis.</summary>
    Function,
}
