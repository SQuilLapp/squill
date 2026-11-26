using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core.Transactions;
using Squill.Presentation.Localization;

namespace Squill.Presentation.Query;

public sealed partial class QueryTabViewModel
{
    /// <summary>The smallest custom auto-refresh interval, in whole seconds.</summary>
    public const int AutoRefreshMinSeconds = 1;

    /// <summary>The largest custom auto-refresh interval, in whole seconds.</summary>
    public const int AutoRefreshMaxSeconds = 3600;

    private ITimer? _autoTimer;
    private Action? _autoCancel;
    private bool _autoActive = true;
    private bool _autoBusy;
    private bool _autoStopped;

    /// <summary>Gets the intervals the clock menu offers: 5 s, 10 s, 30 s, 1 min and 5 min.</summary>
    public static IReadOnlyList<TimeSpan> AutoRefreshPresets { get; } =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
    ];

    /// <summary>Gets the time between the end of a refresh and the next one, or <see langword="null"/> when auto-refresh is disabled; kept in memory only.</summary>
    [ObservableProperty]
    public partial TimeSpan? AutoRefreshInterval { get; private set; }

    /// <summary>Gets why the last tick did not refresh (pending changes, an open editor, a failed transaction, a busy session), or an empty string.</summary>
    [ObservableProperty]
    public partial string AutoRefreshPausedReason { get; private set; } = "";

    /// <summary>Gets whether the interval is set but is not one of <see cref="AutoRefreshPresets"/>.</summary>
    public bool IsAutoRefreshCustom => AutoRefreshInterval is { } i && !AutoRefreshPresets.Contains(i);

    /// <summary>Gets the short text shown next to the clock: empty when disabled, else the interval (<c>30 s</c>, <c>5 m</c>).</summary>
    public string AutoRefreshLabel => AutoRefreshInterval is { } i ? FormatInterval(i) : "";

    /// <summary>Gets the tooltip of the clock: the state on one line, then the pause reason or why it is unavailable.</summary>
    public string AutoRefreshToolTip
    {
        get
        {
            var state = AutoRefreshInterval is { } i
                ? UiText.Format("query.autoRefresh.every", FormatInterval(i))
                : UiText.Get("query.autoRefresh.off");
            var note = AutoRefreshPausedReason.Length > 0 ? AutoRefreshPausedReason
                : !CanRefresh ? UiText.Get("query.autoRefresh.unavailable")
                : "";
            return note.Length == 0 ? state : state + "\n" + note;
        }
    }

    /// <summary>Formats an interval for the label of the clock: whole minutes as <c>5 m</c>, anything else in seconds.</summary>
    /// <param name="interval">The interval.</param>
    /// <returns>The text, in the language of the interface.</returns>
    public static string FormatInterval(TimeSpan interval)
    {
        var seconds = (int)interval.TotalSeconds;
        return seconds >= 60 && seconds % 60 == 0
            ? UiText.Format("query.autoRefresh.minutes", seconds / 60)
            : UiText.Format("query.autoRefresh.seconds", seconds);
    }

    internal Task Idle { get; private set; } = Task.CompletedTask;

    partial void OnAutoRefreshIntervalChanged(TimeSpan? value)
    {
        OnPropertyChanged(nameof(IsAutoRefreshCustom));
        OnPropertyChanged(nameof(AutoRefreshLabel));
        OnPropertyChanged(nameof(AutoRefreshToolTip));
    }

    partial void OnAutoRefreshPausedReasonChanged(string value) => OnPropertyChanged(nameof(AutoRefreshToolTip));

    /// <summary>Selects the auto-refresh interval; the first refresh comes one interval later.</summary>
    /// <param name="interval">The interval (1 to 3600 seconds), or <see langword="null"/> to disable; ignored while <see cref="CanRefresh"/> is false.</param>
    [RelayCommand]
    private void SetAutoRefresh(TimeSpan? interval)
    {
        if (_autoStopped)
            return;
        if (interval is null)
        {
            DisableAutoRefresh();
            return;
        }

        if (!CanRefresh || interval < TimeSpan.FromSeconds(AutoRefreshMinSeconds) || interval > TimeSpan.FromSeconds(AutoRefreshMaxSeconds))
            return;
        AutoRefreshInterval = interval;
        ArmAutoRefresh();
    }

    /// <summary>Asks for a number of seconds and selects it as the auto-refresh interval.</summary>
    [RelayCommand]
    private async Task SetCustomAutoRefreshAsync()
    {
        var title = UiText.Get("query.autoRefresh.customTitle");
        var current = AutoRefreshInterval is { } i ? ((int)i.TotalSeconds).ToString(CultureInfo.InvariantCulture) : "";
        if (await _dialogs.PromptAsync(title, UiText.Get("query.autoRefresh.customPrompt"), current) is not { } text)
            return;
        if (!int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || seconds is < AutoRefreshMinSeconds or > AutoRefreshMaxSeconds)
        {
            await _dialogs.ShowErrorAsync(title, UiText.Get("query.autoRefresh.customInvalid"));
            return;
        }

        SetAutoRefresh(TimeSpan.FromSeconds(seconds));
    }

    /// <summary>Tells the tab whether it is the one on screen; a hidden tab does not refresh, a shown one waits a full interval.</summary>
    /// <param name="active"><see langword="true"/> when the tab is shown.</param>
    public void SetActive(bool active)
    {
        if (_autoActive == active)
            return;
        _autoActive = active;
        if (active)
            ArmAutoRefresh();
        else
            _autoTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>One timer tick: refreshes the last run unless the tab is hidden, disabled, stopped or already refreshing.</summary>
    internal void Tick()
    {
        if (_autoStopped || !_autoActive || _autoBusy || AutoRefreshInterval is null)
            return;
        _autoBusy = true;
        Idle = AutoRefreshAsync();
    }

    /// <summary>Disables auto-refresh for good and releases its timer; called when the tab closes.</summary>
    internal void StopAutoRefresh()
    {
        _autoStopped = true;
        DisableAutoRefresh();
        _autoTimer?.Dispose();
        _autoTimer = null;
    }

    private async Task AutoRefreshAsync()
    {
        var outcome = RefreshOutcome.Failed;
        using var cts = new CancellationTokenSource();
        _autoCancel = cts.Cancel;
        try
        {
            outcome = await RefreshAsync(cts.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // shortcut: the cause is not shown; RefreshAsync reports statement errors itself, so this is only a settings or driver fault.
        }
        finally
        {
            _autoCancel = null;
            _autoBusy = false;
        }

        if (_autoStopped || AutoRefreshInterval is null)
            return;
        switch (outcome)
        {
            case RefreshOutcome.Refreshed:
                AutoRefreshPausedReason = "";
                ArmAutoRefresh();
                break;
            case RefreshOutcome.Skipped:
                AutoRefreshPausedReason = UiText.Get(SkipReasonKey());
                ArmAutoRefresh();
                break;
            case RefreshOutcome.Failed:
                DisableAutoRefresh();
                StatusText = UiText.Get("query.autoRefresh.stopped");
                break;
            default:
                DisableAutoRefresh();
                break;
        }
    }

    private string SkipReasonKey() =>
        HasPendingChanges ? "query.autoRefresh.paused.pending"
        : IsParametersPanelOpen || Results.Any(r => r.EditController?.IsEditing == true || r.Preview?.IsDirty == true) ? "query.autoRefresh.paused.editing"
        : Transaction.State == TransactionState.Failed ? "query.autoRefresh.paused.transaction"
        : "query.autoRefresh.paused.busy";

    private void ArmAutoRefresh()
    {
        if (_autoStopped || !_autoActive || AutoRefreshInterval is not { } interval)
            return;
        _autoTimer ??= _time.CreateTimer(_ => _dispatcher.Post(Tick), this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _autoTimer.Change(interval, Timeout.InfiniteTimeSpan);
    }

    private void DisableAutoRefresh()
    {
        _autoTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _autoCancel?.Invoke();
        AutoRefreshInterval = null;
        AutoRefreshPausedReason = "";
    }

    // After a manual run: a refreshable run keeps the interval and re-arms it, anything else disables it.
    private void AutoRefreshAfterRun()
    {
        if (AutoRefreshInterval is null)
            return;
        if (CanRefresh)
            ArmAutoRefresh();
        else
            DisableAutoRefresh();
    }
}
