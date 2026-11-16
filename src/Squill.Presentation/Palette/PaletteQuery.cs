namespace Squill.Presentation.Palette;

/// <summary>Turns the text typed in the palette into a mode and a search term, and back.</summary>
public static class PaletteQuery
{
    /// <summary>Splits the typed text into its mode, its search term and the prefix character used.</summary>
    /// <param name="text">The text of the palette input.</param>
    /// <returns>The mode, the term (the rest of the text without leading spaces) and the prefix (empty for <see cref="PaletteMode.All"/>).</returns>
    public static (PaletteMode Mode, string Term, string Prefix) Parse(string? text)
    {
        text ??= "";
        if (text.Length == 0)
        {
            return (PaletteMode.All, "", "");
        }

        PaletteMode mode = ModeOf(text[0]);
        return mode == PaletteMode.All
            ? (PaletteMode.All, text, "")
            : (mode, text[1..].TrimStart(' '), text[..1]);
    }

    /// <summary>Rewrites the typed text for a mode pill click: the existing prefix is replaced and the term is kept.</summary>
    /// <param name="text">The current text of the palette input.</param>
    /// <param name="mode">The mode to switch to.</param>
    /// <returns>The prefix of <paramref name="mode"/> followed by the term of <paramref name="text"/>.</returns>
    public static string WithMode(string? text, PaletteMode mode) => PrefixOf(mode) + Parse(text).Term;

    /// <summary>Reads the line number of a <see cref="PaletteMode.Line"/> term.</summary>
    /// <param name="term">The term, which must be made of digits only.</param>
    /// <param name="line">The 1-based line number when the term is valid.</param>
    /// <returns><see langword="true"/> when <paramref name="term"/> is a number.</returns>
    public static bool TryLine(string? term, out int line)
    {
        line = 0;
        if (string.IsNullOrEmpty(term))
        {
            return false;
        }

        foreach (char c in term)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return int.TryParse(term, out line);
    }

    private static PaletteMode ModeOf(char c) => c switch
    {
        '>' => PaletteMode.Actions,
        '#' => PaletteMode.Objects,
        ':' => PaletteMode.Line,
        '!' => PaletteMode.History,
        _ => PaletteMode.All,
    };

    private static string PrefixOf(PaletteMode mode) => mode switch
    {
        PaletteMode.Actions => ">",
        PaletteMode.Objects => "#",
        PaletteMode.Line => ":",
        PaletteMode.History => "!",
        _ => "",
    };
}
