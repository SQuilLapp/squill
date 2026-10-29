namespace Squill.Sql.Statements;

/// <summary>A statement inside a script, trimmed of surrounding whitespace and comments.</summary>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters, including the terminating <c>;</c> when present.</param>
public readonly record struct SqlStatementSpan(int Start, int Length)
{
    /// <summary>Gets the offset just after the last character.</summary>
    public int End => Start + Length;
}
