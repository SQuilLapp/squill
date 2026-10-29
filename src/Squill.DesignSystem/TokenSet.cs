namespace Squill.DesignSystem;

/// <summary>Design tokens read from tokens/squill.tokens.json (W3C Design Tokens format).</summary>
/// <param name="Themes">CSS color values by token name, keyed by theme ("dark" or "light").</param>
/// <param name="Metrics">Sizes and durations shared by all themes.</param>
public sealed record TokenSet(
    Dictionary<string, Dictionary<string, string>> Themes,
    Metrics Metrics);

/// <summary>Sizes and durations of the design system, each keyed by density or scale name.</summary>
/// <param name="RowHeight">Row height in pixels by density.</param>
/// <param name="ControlHeight">Control height in pixels by density.</param>
/// <param name="Radius">Corner radius in pixels by size name.</param>
/// <param name="MotionMs">Animation duration in milliseconds by speed name.</param>
public sealed record Metrics(
    Dictionary<string, int> RowHeight,
    Dictionary<string, int> ControlHeight,
    Dictionary<string, int> Radius,
    Dictionary<string, int> MotionMs);
