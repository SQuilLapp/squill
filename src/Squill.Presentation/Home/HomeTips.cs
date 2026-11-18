using Squill.Presentation.Localization;

namespace Squill.Presentation.Home;

/// <summary>One piece of a tip: plain text, or a keyboard shortcut the heads draw as a chip.</summary>
/// <param name="Text">The text or the shortcut.</param>
/// <param name="IsKey"><see langword="true"/> when <paramref name="Text"/> is a shortcut.</param>
public sealed record TipSegment(string Text, bool IsKey);

/// <summary>The tips of the home page; each one describes a feature the application has.</summary>
public static class HomeTips
{
    /// <summary>Gets the catalog keys of the tips; a <c>{kbd:Ctrl+K}</c> marker in a text becomes a key chip.</summary>
    public static IReadOnlyList<string> Keys { get; } =
    [
        "home.tip.runStatement", "home.tip.runScript", "home.tip.palette", "home.tip.gridFilter", "home.tip.transaction",
        "home.tip.explain", "home.tip.diagram", "home.tip.history", "home.tip.snippets", "home.tip.environment",
    ];

    /// <summary>Splits a tip text into text and key segments.</summary>
    /// <param name="text">Tip text with optional <c>{kbd:...}</c> markers.</param>
    /// <returns>The segments in order; an unclosed marker stays plain text.</returns>
    public static IReadOnlyList<TipSegment> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        const string open = "{kbd:";
        List<TipSegment> segments = [];
        var position = 0;
        while (position < text.Length)
        {
            var start = text.IndexOf(open, position, StringComparison.Ordinal);
            var end = start < 0 ? -1 : text.IndexOf('}', start);
            if (end < 0)
            {
                segments.Add(new TipSegment(text[position..], false));
                break;
            }

            if (start > position)
                segments.Add(new TipSegment(text[position..start], false));
            segments.Add(new TipSegment(text[(start + open.Length)..end], true));
            position = end + 1;
        }

        return segments;
    }
}
