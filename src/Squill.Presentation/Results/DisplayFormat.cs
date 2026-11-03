using System.Globalization;
using Squill.Presentation.Localization;

namespace Squill.Presentation.Results;

/// <summary>Formats the numbers, dates and durations shown to the user with <see cref="UiText.Culture"/>; machine formats stay in <see cref="CellFormatter"/>.</summary>
public static class DisplayFormat
{
    /// <summary>Formats a count with the group separator of the current language.</summary>
    /// <param name="value">The count.</param>
    /// <returns>For example <c>1 234 567</c> in French and <c>1,234,567</c> in English.</returns>
    public static string Count(long value) => value.ToString("N0", UiText.Culture);

    /// <summary>Formats a duration compactly, as milliseconds below one second and seconds above.</summary>
    /// <param name="elapsed">The duration.</param>
    /// <returns>For example <c>120 ms</c> or <c>1,2 s</c> in French, <c>1.2 s</c> in English.</returns>
    public static string Duration(TimeSpan elapsed) => elapsed.TotalSeconds < 1
        ? string.Create(UiText.Culture, $"{elapsed.TotalMilliseconds:0} ms")
        : string.Create(UiText.Culture, $"{elapsed.TotalSeconds:0.0} s");

    /// <summary>Formats a stopwatch reading as minutes, seconds and milliseconds.</summary>
    /// <param name="elapsed">The duration.</param>
    /// <returns>For example <c>0:01,234</c> in French and <c>0:01.234</c> in English.</returns>
    public static string Clock(TimeSpan elapsed) =>
        elapsed.ToString(@"m\:ss", UiText.Culture) + UiText.Culture.NumberFormat.NumberDecimalSeparator + elapsed.ToString("fff", UiText.Culture);

    /// <summary>Formats a moment with the short date and time pattern, or the time alone for a moment of the same day as the reference.</summary>
    /// <param name="at">The moment, already in local time.</param>
    /// <param name="today">The reference moment, in local time.</param>
    /// <returns>The text in the current language's culture.</returns>
    public static string RecentTime(DateTimeOffset at, DateTimeOffset today) =>
        at.ToString(at.Date == today.Date ? "t" : "g", UiText.Culture);
}
