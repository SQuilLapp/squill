using System.Globalization;
using System.Text;

namespace Squill.Presentation.Palette;

/// <summary>Scored, case- and accent-insensitive subsequence matching for the palette. Stateless and thread safe.</summary>
public static class FuzzyMatcher
{
    private const int MaxLength = 256;
    private const int NoMatch = int.MinValue;

    /// <summary>Matches <paramref name="pattern"/> against <paramref name="candidate"/>.</summary>
    /// <param name="candidate">The text to search; only its first 256 characters are scored.</param>
    /// <param name="pattern">The typed pattern; words separated by whitespace must each match, in order.</param>
    /// <returns>The score and the matched ranges in the original candidate, or <see langword="null"/> when the pattern does not match.</returns>
    public static FuzzyMatch? Match(string candidate, string pattern)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(pattern);

        Span<char> pat = stackalloc char[MaxLength];
        Span<int> patMap = stackalloc int[MaxLength];
        pat = pat[..Fold(pattern, pat, patMap)];
        if (pat.Trim().IsEmpty)
        {
            return new FuzzyMatch(0, []);
        }

        string text = candidate.Length > MaxLength ? candidate[..MaxLength] : candidate;
        Span<char> cand = stackalloc char[MaxLength];
        Span<int> map = stackalloc int[MaxLength];
        int len = Fold(text, cand, map);
        cand = cand[..len];

        Span<bool> wordStart = stackalloc bool[MaxLength];
        for (int i = 0; i < len; i++)
        {
            wordStart[i] = i == 0
                || cand[i - 1] is ' ' or '_' or '.' or '-' or '/'
                || (char.IsLower(text[map[i - 1]]) && char.IsUpper(text[map[i]]));
        }

        Span<int> positions = stackalloc int[MaxLength];
        int count = 0;
        int from = 0;
        int score = 0;
        for (int p = 0; p < pat.Length;)
        {
            if (char.IsWhiteSpace(pat[p]))
            {
                p++;
                continue;
            }

            int e = p;
            while (e < pat.Length && !char.IsWhiteSpace(pat[e]))
            {
                e++;
            }

            ReadOnlySpan<char> word = pat[p..e];
            if (!MatchWord(cand, wordStart, word, from, positions[count..], out int wordScore))
            {
                return null;
            }

            score += wordScore;
            count += word.Length;
            from = positions[count - 1] + 1;
            p = e;
        }

        score -= len / 8;
        return new FuzzyMatch(score, ToRanges(positions[..count], map[..len], text.Length));
    }

    // shortcut: greedy with one retry from the best word start, upgrade if ordering feels wrong.
    private static bool MatchWord(ReadOnlySpan<char> cand, ReadOnlySpan<bool> wordStart, ReadOnlySpan<char> word, int from, Span<int> positions, out int score)
    {
        score = 0;
        if (from >= cand.Length)
        {
            return false;
        }

        // Contiguous occurrence, preferring one at a word start.
        int found = -1;
        for (int at = from; at <= cand.Length - word.Length;)
        {
            int k = cand[at..].IndexOf(word);
            if (k < 0)
            {
                break;
            }

            k += at;
            if (found < 0)
            {
                found = k;
            }

            if (wordStart[k])
            {
                found = k;
                break;
            }

            at = k + 1;
        }

        if (found >= 0)
        {
            for (int i = 0; i < word.Length; i++)
            {
                positions[i] = found + i;
            }

            int tier = found == 0 ? (word.Length == cand.Length ? 1000 : 500) : wordStart[found] ? 200 : 100;
            score = tier + Score(positions[..word.Length], wordStart);
            return true;
        }

        // Scattered subsequence: greedy from the first occurrence, retried from the first word-start one.
        int first = cand[from..].IndexOf(word[0]);
        if (first < 0)
        {
            return false;
        }

        first += from;
        int best = NoMatch;
        Span<int> trial = stackalloc int[word.Length];
        for (int attempt = 0; attempt < 2; attempt++)
        {
            int start = first;
            if (attempt == 1)
            {
                start = -1;
                for (int i = first + 1; i < cand.Length; i++)
                {
                    if (cand[i] == word[0] && wordStart[i])
                    {
                        start = i;
                        break;
                    }
                }

                if (start < 0)
                {
                    break;
                }
            }

            trial[0] = start;
            int cur = start + 1;
            bool ok = true;
            for (int i = 1; i < word.Length; i++)
            {
                int k = cur < cand.Length ? cand[cur..].IndexOf(word[i]) : -1;
                if (k < 0)
                {
                    ok = false;
                    break;
                }

                trial[i] = cur + k;
                cur = trial[i] + 1;
            }

            if (!ok)
            {
                continue;
            }

            int s = Score(trial, wordStart);
            if (s > best)
            {
                best = s;
                trial.CopyTo(positions);
            }
        }

        score = best;
        return best != NoMatch;
    }

    private static int Score(ReadOnlySpan<int> pos, ReadOnlySpan<bool> wordStart)
    {
        int s = -Math.Min(pos[0], 10);
        for (int i = 0; i < pos.Length; i++)
        {
            s += 10;
            if (wordStart[pos[i]])
            {
                s += 20;
            }

            if (i > 0)
            {
                int gap = pos[i] - pos[i - 1] - 1;
                s += gap == 0 ? 15 : -gap;
            }
        }

        return s;
    }

    private static List<(int Start, int Length)> ToRanges(ReadOnlySpan<int> pos, ReadOnlySpan<int> map, int textLength)
    {
        List<(int Start, int Length)> ranges = [];
        int runStart = -1;
        int runEnd = -1;
        for (int i = 0; i < pos.Length; i++)
        {
            int start = map[pos[i]];
            int end = pos[i] + 1 < map.Length ? map[pos[i] + 1] : textLength;
            if (runStart >= 0 && start == runEnd)
            {
                runEnd = end;
                continue;
            }

            if (runStart >= 0)
            {
                ranges.Add((runStart, runEnd - runStart));
            }

            runStart = start;
            runEnd = end;
        }

        if (runStart >= 0)
        {
            ranges.Add((runStart, runEnd - runStart));
        }

        return ranges;
    }

    // Lower-cases and strips diacritics one character at a time; combining marks are dropped, so map[i] is the original index of folded[i].
    private static int Fold(string text, Span<char> folded, Span<int> map)
    {
        int n = 0;
        for (int i = 0; i < text.Length && n < folded.Length; i++)
        {
            char c = text[i];
            if (c < 128)
            {
                folded[n] = char.ToLowerInvariant(c);
                map[n++] = i;
                continue;
            }

            if (n > 0 && char.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            char b = c;
            if (!char.IsSurrogate(c))
            {
                foreach (char x in c.ToString().Normalize(NormalizationForm.FormD))
                {
                    if (char.GetUnicodeCategory(x) != UnicodeCategory.NonSpacingMark)
                    {
                        b = x;
                        break;
                    }
                }
            }

            folded[n] = char.ToLowerInvariant(b);
            map[n++] = i;
        }

        return n;
    }
}
