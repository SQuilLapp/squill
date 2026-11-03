using System.Globalization;

namespace Squill.Presentation.Localization;

/// <summary>Renders the coded messages of Core and Sql (<c>CoreMessageCodes</c>, <c>SqlMessageCodes</c>) in the current language.</summary>
public static class IssueText
{
    /// <summary>Renders a coded message from the <c>issue.&lt;code&gt;</c> catalog entry.</summary>
    /// <param name="code">The message code, or null when the producer set none.</param>
    /// <param name="args">Values for the placeholders; when the catalog has <c>.one</c> / <c>.other</c> texts, <c>args[0]</c> is the count.</param>
    /// <param name="fallback">The English message, returned when <paramref name="code"/> is null or has no catalog entry.</param>
    /// <returns>The text in the current language.</returns>
    public static string Format(string? code, IReadOnlyList<string>? args, string fallback)
    {
        if (code is null)
        {
            return fallback;
        }

        var key = "issue." + code;
        object?[] values = args is null ? [] : [.. args];
        if (UiText.Has(key))
        {
            return UiText.Format(key, values);
        }

        return UiText.Has(key + ".other") && values.Length > 0 && int.TryParse((string?)values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            ? UiText.FormatPlural(key, count, values[1..])
            : fallback;
    }
}
