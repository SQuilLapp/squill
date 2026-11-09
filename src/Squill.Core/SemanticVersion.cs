using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Squill.Core;

/// <summary>A Semantic Versioning 2.0 version; build metadata is accepted when parsing and dropped.</summary>
/// <param name="Major">The major number.</param>
/// <param name="Minor">The minor number.</param>
/// <param name="Patch">The patch number.</param>
/// <param name="PreRelease">The dot-separated pre-release identifiers, or an empty string for a release.</param>
public readonly record struct SemanticVersion(int Major, int Minor, int Patch, string PreRelease)
    : IComparable<SemanticVersion>, IParsable<SemanticVersion>
{
    /// <summary>Gets a value indicating whether this version carries a pre-release suffix.</summary>
    public bool IsPreRelease => !string.IsNullOrEmpty(PreRelease);

    /// <summary>Parses a version such as <c>1.2.3</c> or <c>1.2.3-beta.1</c>; a leading <c>v</c> is not accepted.</summary>
    /// <param name="text">The text to parse.</param>
    /// <returns>The parsed version.</returns>
    /// <exception cref="FormatException"><paramref name="text"/> is not a valid Semantic Versioning 2.0 version.</exception>
    public static SemanticVersion Parse(string text) => Parse(text, CultureInfo.InvariantCulture);

    /// <inheritdoc cref="Parse(string)"/>
    /// <param name="s">The text to parse.</param>
    /// <param name="provider">Ignored; versions are culture-independent.</param>
    public static SemanticVersion Parse(string s, IFormatProvider? provider) =>
        TryParse(s, out var version) ? version : throw new FormatException($"'{s}' is not a valid semantic version.");

    /// <summary>Tries to parse a version without throwing.</summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="result">The parsed version, or the default value on failure.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is a valid version.</returns>
    public static bool TryParse([NotNullWhen(true)] string? text, out SemanticVersion result)
    {
        result = default;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var rest = text.AsSpan();
        var plus = rest.IndexOf('+');
        if (plus >= 0)
        {
            if (!AreIdentifiers(rest[(plus + 1)..], allowLeadingZeros: true))
            {
                return false;
            }

            rest = rest[..plus];
        }

        var dash = rest.IndexOf('-');
        var pre = ReadOnlySpan<char>.Empty;
        if (dash >= 0)
        {
            pre = rest[(dash + 1)..];
            if (!AreIdentifiers(pre, allowLeadingZeros: false))
            {
                return false;
            }

            rest = rest[..dash];
        }

        var parts = rest;
        var firstDot = parts.IndexOf('.');
        if (firstDot < 0)
        {
            return false;
        }

        var tail = parts[(firstDot + 1)..];
        var secondDot = tail.IndexOf('.');
        if (secondDot < 0
            || !TryNumber(parts[..firstDot], out var major)
            || !TryNumber(tail[..secondDot], out var minor)
            || !TryNumber(tail[(secondDot + 1)..], out var patch))
        {
            return false;
        }

        result = new SemanticVersion(major, minor, patch, pre.ToString());
        return true;
    }

    /// <inheritdoc cref="TryParse(string?, out SemanticVersion)"/>
    /// <param name="s">The text to parse.</param>
    /// <param name="provider">Ignored; versions are culture-independent.</param>
    /// <param name="result">The parsed version, or the default value on failure.</param>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out SemanticVersion result) =>
        TryParse(s, out result);

    /// <summary>Compares two versions with SemVer 2 precedence.</summary>
    /// <param name="other">The version to compare with.</param>
    /// <returns>A negative number, zero or a positive number when this version is lower, equal or higher.</returns>
    public int CompareTo(SemanticVersion other)
    {
        var c = Major.CompareTo(other.Major);
        if (c == 0)
        {
            c = Minor.CompareTo(other.Minor);
        }

        if (c == 0)
        {
            c = Patch.CompareTo(other.Patch);
        }

        return c != 0 ? c : ComparePreRelease(PreRelease ?? "", other.PreRelease ?? "");
    }

    /// <summary>Gets a value indicating whether <paramref name="left"/> has lower precedence than <paramref name="right"/>.</summary>
    /// <param name="left">The first version.</param>
    /// <param name="right">The second version.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is lower.</returns>
    public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;

    /// <summary>Gets a value indicating whether <paramref name="left"/> has higher precedence than <paramref name="right"/>.</summary>
    /// <param name="left">The first version.</param>
    /// <param name="right">The second version.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is higher.</returns>
    public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;

    /// <summary>Gets a value indicating whether <paramref name="left"/> is lower than or equal to <paramref name="right"/>.</summary>
    /// <param name="left">The first version.</param>
    /// <param name="right">The second version.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is not higher.</returns>
    public static bool operator <=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) <= 0;

    /// <summary>Gets a value indicating whether <paramref name="left"/> is higher than or equal to <paramref name="right"/>.</summary>
    /// <param name="left">The first version.</param>
    /// <param name="right">The second version.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is not lower.</returns>
    public static bool operator >=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) >= 0;

    /// <summary>Formats the version as <c>1.2.3</c> or <c>1.2.3-beta.1</c>.</summary>
    /// <returns>The version text.</returns>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}{(IsPreRelease ? "-" + PreRelease : "")}");

    private static int ComparePreRelease(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0)
        {
            // A release outranks its own pre-releases.
            return a.Length == b.Length ? 0 : a.Length == 0 ? 1 : -1;
        }

        var left = a.Split('.');
        var right = b.Split('.');
        for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            var c = CompareIdentifier(left[i], right[i]);
            if (c != 0)
            {
                return c;
            }
        }

        return left.Length.CompareTo(right.Length);
    }

    private static int CompareIdentifier(string a, string b)
    {
        var aNum = IsNumeric(a);
        var bNum = IsNumeric(b);
        if (aNum && bNum)
        {
            // No leading zeros, so a longer number is larger and equal lengths order ordinally; no overflow.
            return a.Length != b.Length ? a.Length.CompareTo(b.Length) : Math.Sign(string.CompareOrdinal(a, b));
        }

        if (aNum != bNum)
        {
            return aNum ? -1 : 1;
        }

        return Math.Sign(string.CompareOrdinal(a, b));
    }

    private static bool IsNumeric(ReadOnlySpan<char> s) => s.IndexOfAnyExceptInRange('0', '9') < 0;

    private static bool TryNumber(ReadOnlySpan<char> s, out int value)
    {
        value = 0;
        return s.Length > 0
            && IsNumeric(s)
            && (s.Length == 1 || s[0] != '0')
            && int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static bool AreIdentifiers(ReadOnlySpan<char> s, bool allowLeadingZeros)
    {
        if (s.IsEmpty)
        {
            return false;
        }

        foreach (var range in s.Split('.'))
        {
            var id = s[range];
            if (id.IsEmpty
                || !IsIdentifier(id)
                || !allowLeadingZeros && id.Length > 1 && id[0] == '0' && IsNumeric(id))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsIdentifier(ReadOnlySpan<char> id)
    {
        foreach (var ch in id)
        {
            if (!char.IsAsciiLetterOrDigit(ch) && ch != '-')
            {
                return false;
            }
        }

        return true;
    }
}
