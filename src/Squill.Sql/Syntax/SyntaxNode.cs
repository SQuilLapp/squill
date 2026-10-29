namespace Squill.Sql.Syntax;

/// <summary>A node of the SQL syntax tree, located by a span of the source text.</summary>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters; zero for a node that stands for missing input.</param>
public abstract record SyntaxNode(int Start, int Length)
{
    /// <summary>Gets the offset just after the last character.</summary>
    public int End => Start + Length;
}

/// <summary>A node that evaluates to a value.</summary>
/// <param name="Start">Zero-based offset of the first character.</param>
/// <param name="Length">Number of characters.</param>
public abstract record Expression(int Start, int Length) : SyntaxNode(Start, Length);
