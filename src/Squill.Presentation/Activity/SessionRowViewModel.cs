using System.Globalization;
using Squill.Core.Activity;
using Squill.Presentation.Localization;

namespace Squill.Presentation.Activity;

/// <summary>Colour class of a figure of the monitor; the head maps it to a theme brush.</summary>
public enum ActivitySeverity
{
    /// <summary>No emphasis.</summary>
    Normal,

    /// <summary>Worth a look (warning colour).</summary>
    Warning,

    /// <summary>Needs attention (danger colour).</summary>
    Danger,
}

/// <summary>Colour of the state dot of a row.</summary>
public enum StateDotKind
{
    /// <summary>Running a statement.</summary>
    Success,

    /// <summary>Idle inside an open transaction.</summary>
    Warning,

    /// <summary>Idle, or any other state.</summary>
    Muted,

    /// <summary>Blocked.</summary>
    Danger,
}

/// <summary>One line of the sessions table.</summary>
public sealed class SessionRowViewModel
{
    /// <summary>The longest statement text kept on a row, so one huge query cannot stretch the table.</summary>
    private const int QueryLineLength = 200;

    internal SessionRowViewModel(ServerSession session, int blockedByThis, bool isBlocked)
    {
        Session = session;
        BlockedCount = blockedByThis;
        IsBlocked = isBlocked;
        StateDot = session.State switch
        {
            SessionState.Active => StateDotKind.Success,
            SessionState.IdleInTransaction => StateDotKind.Warning,
            SessionState.Waiting => StateDotKind.Danger,
            _ => StateDotKind.Muted,
        };
        DurationText = FormatDuration(session.Duration);
        DurationSeverity = session.Duration is { } d && session.State == SessionState.Active
            ? d > SessionsMonitorViewModel.VeryLongQuery ? ActivitySeverity.Danger
            : d > SessionsMonitorViewModel.LongQuery ? ActivitySeverity.Warning : ActivitySeverity.Normal
            : ActivitySeverity.Normal;
        QueryLine = OneLine(session.Query);
    }

    /// <summary>Gets the session the row shows.</summary>
    public ServerSession Session { get; }

    /// <summary>Gets the engine session id (the pid).</summary>
    public string Id => Session.Id;

    /// <summary>Gets the login, or an empty text when not visible.</summary>
    public string User => Session.User ?? "";

    /// <summary>Gets the client application name, or an empty text.</summary>
    public string Application => Session.Application ?? "";

    /// <summary>Gets the client address, or an empty text.</summary>
    public string Client => Session.Client ?? "";

    /// <summary>Gets the engine's own wording of the state.</summary>
    public string StateText => Session.StateText;

    /// <summary>Gets the colour of the state dot.</summary>
    public StateDotKind StateDot { get; }

    /// <summary>Gets the duration as <c>6 min 12 s</c>, <c>48 s</c>, <c>400 ms</c> or <c>-</c>.</summary>
    public string DurationText { get; }

    /// <summary>Gets the colour of the duration: warning above 30 s and danger above 5 min for a running statement.</summary>
    public ActivitySeverity DurationSeverity { get; }

    /// <summary>Gets what the session waits for, or an empty text.</summary>
    public string WaitText => Session.WaitEvent ?? "";

    /// <summary>Gets the statement on one line, cut to a bounded length.</summary>
    public string QueryLine { get; }

    /// <summary>Gets how many sessions wait behind this one, directly or not.</summary>
    public int BlockedCount { get; }

    /// <summary>Gets a value indicating whether another session blocks this one.</summary>
    public bool IsBlocked { get; }

    /// <summary>Formats a duration for the table.</summary>
    /// <param name="duration">The duration, or <see langword="null"/>.</param>
    /// <returns>The text; <c>-</c> when unknown.</returns>
    public static string FormatDuration(TimeSpan? duration)
    {
        if (duration is not { } d)
            return "-";
        if (d < TimeSpan.FromSeconds(1))
            return UiText.Format("activity.duration.ms", Math.Max(0, (int)d.TotalMilliseconds));
        if (d < TimeSpan.FromMinutes(1))
            return UiText.Format("activity.duration.s", (int)d.TotalSeconds);
        if (d < TimeSpan.FromHours(1))
            return UiText.Format("activity.duration.minS", (int)d.TotalMinutes, d.Seconds);
        return UiText.Format("activity.duration.hMin", (int)d.TotalHours, d.Minutes.ToString("00", CultureInfo.InvariantCulture));
    }

    private static string OneLine(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "";
        var line = string.Join(' ', query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return line.Length <= QueryLineLength ? line : line[..QueryLineLength] + "…";
    }
}
