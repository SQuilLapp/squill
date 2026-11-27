namespace Squill.Presentation.Explorer;

/// <summary>Case-insensitive wildcard matching for explorer filters: <c>*</c> any run of characters, <c>?</c> exactly one.</summary>
public static class WildcardPattern
{
    /// <summary>Tells whether the whole <paramref name="name"/> matches <paramref name="pattern"/>.</summary>
    /// <param name="pattern">Pattern where <c>*</c> matches any run (empty included), <c>?</c> one character, anything else itself.</param>
    /// <param name="name">Name to test.</param>
    /// <returns><see langword="true"/> when the name matches.</returns>
    public static bool IsMatch(string pattern, string name)
    {
        int p = 0, n = 0, star = -1, mark = 0;
        while (n < name.Length)
        {
            if (p < pattern.Length && pattern[p] == '*')
            {
                star = p++;
                mark = n;
            }
            else if (p < pattern.Length && (pattern[p] == '?' || char.ToUpperInvariant(pattern[p]) == char.ToUpperInvariant(name[n])))
            {
                p++;
                n++;
            }
            else if (star >= 0)
            {
                p = star + 1;
                n = ++mark;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
        {
            p++;
        }

        return p == pattern.Length;
    }

    /// <summary>Tells whether the pattern contains <c>*</c> or <c>?</c>.</summary>
    /// <param name="pattern">Pattern to inspect.</param>
    /// <returns><see langword="true"/> when it is not a plain name.</returns>
    public static bool HasWildcard(string pattern) => pattern.AsSpan().IndexOfAny('*', '?') >= 0;
}
