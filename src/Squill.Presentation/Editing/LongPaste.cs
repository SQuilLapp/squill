namespace Squill.Presentation.Editing;

/// <summary>Tells when a pasted text is long enough that its managed processing leaves the UI thread and the heads show a progress popup.</summary>
public static class LongPaste
{
    /// <summary>The length, in UTF-16 code units, from which a text counts as a long paste (about 100 KB of ASCII).</summary>
    public const int Threshold = 100_000;

    /// <summary>Returns whether a text is a long paste.</summary>
    /// <param name="text">The clipboard text, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> has at least <see cref="Threshold"/> characters.</returns>
    public static bool IsLong(string? text) => text is not null && text.Length >= Threshold;
}
