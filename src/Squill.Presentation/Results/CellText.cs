using System.Buffers;

namespace Squill.Presentation.Results;

/// <summary>Display-only text rules for grid cells: one line, bounded length. Also what the in-place editor shows, read-only, for a multi-line value. Never used for the clipboard, export, the preview pane or commits.</summary>
public static class CellText
{
    /// <summary>Most characters a grid cell ever draws or measures (the last one is the ellipsis when the text was cut).</summary>
    public const int PreviewLength = 500;

    private const int ScanLimit = 8 * PreviewLength;

    private static readonly SearchValues<char> s_lineBreaks = SearchValues.Create("\r\n\u000B\u000C\u0085\u2028\u2029");

    private static readonly SearchValues<char> s_flattened = SearchValues.Create(CreateFlattened());

    /// <summary>Returns whether <paramref name="text"/> holds no line break (CR, LF, VT, FF, NEL, LS or PS; tabs are fine).</summary>
    /// <param name="text">The text to test.</param>
    /// <returns><see langword="true"/> when a one-line box can hold the text as is.</returns>
    public static bool IsSingleLine(string text) => text.AsSpan().IndexOfAny(s_lineBreaks) < 0;

    /// <summary>Flattens <paramref name="text"/> to one line of at most <see cref="PreviewLength"/> characters.</summary>
    /// <param name="text">The full cell text.</param>
    /// <returns>The same instance when nothing needs changing; otherwise runs holding a line break, tab or other control or white-space character other than plain spaces become one space and a too-long text ends with an ellipsis.</returns>
    public static string Preview(string text)
    {
        if (text.Length <= PreviewLength && text.AsSpan().IndexOfAny(s_flattened) < 0)
        {
            return text;
        }

        Span<char> buffer = stackalloc char[PreviewLength + 1];
        var n = 0;
        var read = 0;
        var truncated = false;
        while (read < text.Length)
        {
            if (read >= ScanLimit)
            {
                truncated = true;
                break;
            }

            var start = read;
            var plainOnly = true;
            while (read < text.Length && read < ScanLimit && IsBlank(text[read]))
            {
                plainOnly &= text[read] == ' ';
                read++;
            }

            if (read > start)
            {
                var run = plainOnly ? read - start : 1;
                for (var i = 0; i < run; i++)
                {
                    if (n == PreviewLength)
                    {
                        truncated = true;
                        break;
                    }

                    buffer[n++] = ' ';
                }

                if (truncated || (read >= ScanLimit && read < text.Length))
                {
                    truncated = true;
                    break;
                }

                continue;
            }

            if (n == PreviewLength)
            {
                truncated = true;
                break;
            }

            buffer[n++] = text[read++];
        }

        if (!truncated)
        {
            return new string(buffer[..n]);
        }

        var keep = Math.Min(n, PreviewLength - 1);
        if (keep > 0 && char.IsHighSurrogate(buffer[keep - 1]))
        {
            keep--;
        }

        buffer[keep] = '…';
        return new string(buffer[..(keep + 1)]);
    }

    private static string CreateFlattened()
    {
        var chars = new char[32 + 4];
        for (var i = 0; i < 32; i++)
        {
            chars[i] = (char)i;
        }

        chars[32] = (char)0x7F;
        chars[33] = (char)0x85;
        chars[34] = (char)0x2028;
        chars[35] = (char)0x2029;
        return new string(chars);
    }

    private static bool IsBlank(char c) => char.IsWhiteSpace(c) || char.IsControl(c);
}
