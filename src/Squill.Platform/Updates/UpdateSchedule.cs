using Squill.Platform.Settings;

namespace Squill.Platform.Updates;

/// <summary>Decides whether an automatic update check is due.</summary>
public static class UpdateSchedule
{
    private static readonly TimeSpan DailyInterval = TimeSpan.FromHours(20);
    private static readonly TimeSpan WeeklyInterval = TimeSpan.FromDays(6.5);

    /// <summary>Tells whether an automatic check should run now.</summary>
    /// <param name="frequency">The user's choice.</param>
    /// <param name="lastCheckUtc">When the last check ran, if ever.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns>
    /// <see langword="false"/> for <see cref="UpdateCheckFrequency.NotAsked"/> and <see cref="UpdateCheckFrequency.Never"/>;
    /// otherwise whether the interval (slightly shorter than a day or a week) has elapsed. A last check in the future counts as due.
    /// </returns>
    public static bool IsDue(UpdateCheckFrequency frequency, DateTimeOffset? lastCheckUtc, DateTimeOffset nowUtc) => frequency switch
    {
        UpdateCheckFrequency.Startup => true,
        UpdateCheckFrequency.Daily => Elapsed(lastCheckUtc, nowUtc, DailyInterval),
        UpdateCheckFrequency.Weekly => Elapsed(lastCheckUtc, nowUtc, WeeklyInterval),
        _ => false,
    };

    private static bool Elapsed(DateTimeOffset? last, DateTimeOffset now, TimeSpan interval) =>
        last is not { } value || value > now || now - value >= interval;
}
