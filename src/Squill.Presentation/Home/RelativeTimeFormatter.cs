using Squill.Presentation.Localization;

namespace Squill.Presentation.Home;

/// <summary>Writes a past instant the way the home page shows it: minutes, hours, "yesterday, 18:42", a weekday, then a date.</summary>
public static class RelativeTimeFormatter
{
    /// <summary>Formats <paramref name="when"/> relative to <paramref name="now"/> in the current <see cref="UiText.Language"/>.</summary>
    /// <param name="when">The instant to describe.</param>
    /// <param name="now">The current local time; its offset decides what "today" and "yesterday" mean.</param>
    /// <returns>For example <c>il y a 12 min</c>, <c>hier, 18:42</c>, <c>lundi</c> or <c>21 oct.</c>; a future instant reads as "now".</returns>
    public static string Format(DateTimeOffset when, DateTimeOffset now)
    {
        var local = when.ToOffset(now.Offset);
        var age = now - local;
        if (age < TimeSpan.FromMinutes(1))
            return UiText.Get("home.time.now");
        if (age < TimeSpan.FromHours(1))
            return UiText.Format("home.time.minutes", (int)age.TotalMinutes);
        var days = now.Date.Subtract(local.Date).Days;
        if (days == 0)
            return UiText.Format("home.time.hours", (int)age.TotalHours);
        if (days == 1)
            return UiText.Format("home.time.yesterday", local.ToString("HH:mm", UiText.Culture));
        if (days < 7)
            return local.ToString("dddd", UiText.Culture);
        return local.ToString(local.Year == now.Year ? "d MMM" : "d MMM yyyy", UiText.Culture);
    }
}
