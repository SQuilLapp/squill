namespace Squill.DesignSystem;

/// <summary>Design tokens read from tokens/squill.tokens.json (W3C Design Tokens format).</summary>
public sealed record TokenSet(
    Dictionary<string, Dictionary<string, string>> Themes,
    Metrics Metrics);

public sealed record Metrics(
    Dictionary<string, int> RowHeight,
    Dictionary<string, int> ControlHeight,
    Dictionary<string, int> Radius,
    Dictionary<string, int> MotionMs);
