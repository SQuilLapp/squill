using Squill.Core;
using Squill.Sql.Lexing;

namespace Squill.Sql.Completion;

/// <summary>Decides the case of the keywords a completion writes, from the user setting and the script around the caret.</summary>
internal static class KeywordCasing
{
    /// <summary>Tells whether keywords are written in lower case for this request.</summary>
    /// <param name="request">The request; <see cref="SqlRequest.KeywordCase"/> is the setting.</param>
    /// <param name="prefixStart">The start of the word being typed.</param>
    /// <param name="prefixLength">The length of the word being typed.</param>
    /// <returns>
    /// <see langword="true"/> for <see cref="KeywordCase.Lower"/>; for <see cref="KeywordCase.Preserve"/> when the keywords of the text are mostly lower case,
    /// or, when it has none, when the typed prefix has letters and all are lower case.
    /// </returns>
    public static bool UseLowerKeywords(SqlRequest request, int prefixStart, int prefixLength)
    {
        if (request.KeywordCase != KeywordCase.Preserve)
        {
            return request.KeywordCase == KeywordCase.Lower;
        }

        int lower = 0, upper = 0;
        var text = request.Text;
        foreach (var t in SqlTokenizers.For(request.Dialect).Tokenize(text))
        {
            if (t.Kind != TokenKind.Keyword)
            {
                continue;
            }

            var word = t.Text(text);
            if (!word.Any(char.IsUpper))
            {
                lower++;
            }
            else if (!word.Any(char.IsLower))
            {
                upper++;
            }
        }

        if (lower + upper > 0)
        {
            return lower > upper;
        }

        var prefix = text.AsSpan(Math.Clamp(prefixStart, 0, text.Length), Math.Clamp(prefixLength, 0, text.Length - Math.Clamp(prefixStart, 0, text.Length)));
        return prefix.ToString() is { Length: > 0 } p && p.Any(char.IsLetter) && !p.Any(char.IsUpper);
    }

    /// <summary>Writes a keyword (possibly several words) in the case decided by <see cref="UseLowerKeywords"/>.</summary>
    /// <param name="canonicalUpper">The upper-case keyword.</param>
    /// <param name="request">The request.</param>
    /// <param name="prefixStart">The start of the word being typed.</param>
    /// <param name="prefixLength">The length of the word being typed.</param>
    /// <returns>The keyword in the chosen case.</returns>
    public static string Apply(string canonicalUpper, SqlRequest request, int prefixStart, int prefixLength)
        => UseLowerKeywords(request, prefixStart, prefixLength) ? canonicalUpper.ToLowerInvariant() : canonicalUpper;
}
