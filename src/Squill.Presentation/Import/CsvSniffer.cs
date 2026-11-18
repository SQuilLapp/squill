using System.Text;

namespace Squill.Presentation.Import;

/// <summary>Guesses the <see cref="CsvOptions"/> of a file from its first bytes.</summary>
public static class CsvSniffer
{
    private const int MaxLines = 50;
    private static readonly char[] Candidates = [',', ';', '\t', '|'];

    /// <summary>Suggests encoding and delimiter for a CSV file.</summary>
    /// <param name="head">The first bytes of the file (a few kilobytes are enough).</param>
    /// <returns>
    /// Options with the encoding taken from the BOM, else UTF-8 when the bytes are valid UTF-8, else Latin-1
    /// (<c>Windows-1252</c> files are read as Latin-1: no code-page provider exists under AOT); the delimiter that gives
    /// a consistent field count over the first 50 lines (comma when none does); quote <c>"</c>; header on.
    /// </returns>
    public static CsvOptions Detect(ReadOnlySpan<byte> head)
    {
        var encoding = DetectEncoding(head);
        var text = encoding.GetString(head).TrimStart('﻿');
        return new CsvOptions(encoding, DetectDelimiter(text), '"', true);
    }

    private static Encoding DetectEncoding(ReadOnlySpan<byte> head)
    {
        if (head.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            return new UTF8Encoding(false);
        }

        if (head.StartsWith(new byte[] { 0xFF, 0xFE }))
        {
            return new UnicodeEncoding(false, false);
        }

        if (head.StartsWith(new byte[] { 0xFE, 0xFF }))
        {
            return new UnicodeEncoding(true, false);
        }

        try
        {
            // flush: false tolerates a multi-byte sequence cut by the end of the sample.
            _ = new UTF8Encoding(false, true).GetDecoder().GetCharCount(head, flush: false);
            return new UTF8Encoding(false);
        }
        catch (DecoderFallbackException)
        {
            // shortcut: Windows-1252 is read as Latin-1 (differs on 0x80-0x9F only), upgrade if a provider ships.
            return Encoding.Latin1;
        }
    }

    private static char DetectDelimiter(string text)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(MaxLines).ToArray();
        var best = ',';
        var bestScore = 0;
        var bestCount = 0;
        foreach (var candidate in Candidates)
        {
            var mode = lines.Select(line => CountFields(line, candidate))
                .GroupBy(c => c)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Key)
                .FirstOrDefault();
            if (mode is null || mode.Key < 2)
            {
                continue;
            }

            var score = mode.Count();
            if (score > bestScore || (score == bestScore && mode.Key > bestCount))
            {
                (best, bestScore, bestCount) = (candidate, score, mode.Key);
            }
        }

        return best;
    }

    private static int CountFields(string line, char delimiter)
    {
        var count = 1;
        var inQuotes = false;
        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == delimiter && !inQuotes)
            {
                count++;
            }
        }

        return count;
    }
}
