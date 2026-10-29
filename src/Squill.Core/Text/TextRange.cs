namespace Squill.Core;

/// <summary>A span of text given by start offset and length.</summary>
public readonly record struct TextRange(int Start, int Length)
{
    /// <summary>Offset just past the last character.</summary>
    public int End => Start + Length;
}
