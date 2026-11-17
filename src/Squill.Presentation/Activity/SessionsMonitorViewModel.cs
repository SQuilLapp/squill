using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Core.Activity;
using Squill.Data;
using Squill.Presentation.Localization;
using Squill.Presentation.Safety;
using Squill.Presentation.Sessions;
using Squill.Sql.Transactions;

namespace Squill.Presentation.Activity;

/// <summary>Which sessions the table lists.</summary>
public enum SessionFilter
{
    /// <summary>Every session.</summary>
    All,

    /// <summary>Sessions running a statement.</summary>
    Active,

    /// <summary>Sessions idle inside an open transaction.</summary>
    IdleInTransaction,

    /// <summary>Sessions waiting for a lock or another resource.</summary>
    Waiting,
}

/// <summary>A column the table can be sorted by.</summary>
public enum SessionSortColumn
{
    /// <summary>The session id.</summary>
    Pid,

    /// <summary>The login.</summary>
    User,

    /// <summary>The application name.</summary>
    Application,

    /// <summary>The client address.</summary>
    Client,

    /// <summary>The state.</summary>
    State,

    /// <summary>How long the statement or state has lasted.</summary>
    Duration,

    /// <summary>What the session waits for.</summary>
    Wait,

    /// <summary>The statement text.</summary>
    Query,
}

/// <summary>
/// The sessions and locks monitor of one connection: it polls an <see cref="IActivityReader"/> while its tab is on screen, never
/// overlaps two reads, and offers cancel / terminate behind the production confirmation.
/// </summary>
public sealed partial class SessionsMonitorViewModel : ViewModelBase, IDisposable
{
    /// <summary>A statement running longer than this is a long query.</summary>
    internal static readonly TimeSpan LongQuery = TimeSpan.FromSeconds(30);

    /// <summary>A statement running longer than this is a very long query.</summary>
    internal static readonly TimeSpan VeryLongQuery = TimeSpan.FromMinutes(5);

    private const int HistoryLength = 60;

    private static readonly int[] Intervals = [1, 2, 5, 10];

    private readonly ConnectionProfile _profile;
    private readonly IActivityReader _reader;
    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _dispatcher;
    private readonly TimeProvider _time;
    private readonly ITimer _timer;
    private readonly List<double> _history = [];
    private ActivitySnapshot? _snapshot;
    private CancellationTokenSource? _readCts;
    private Task _current = Task.CompletedTask;
    private int _sequence;
    private bool _inFlight;
    private bool _tabActive;
    private bool _disposed;
    private long? _previousCounter;
    private DateTimeOffset _previousAt;
    private DateTimeOffset? _appliedAt;
    private string? _selectedId;
    private Dictionary<string, SessionRowViewModel> _rowsById = [];
    private bool _sortDescending = true;
    private int[] _counts = [0, 0, 0, 0];

    /// <summary>Creates the monitor of a connected session.</summary>
    /// <param name="session">The session; its connection must implement <see cref="IActivityReader"/>.</param>
    /// <param name="dialogs">Used for the confirmations and the error messages.</param>
    /// <param name="dispatcher">Brings the timer ticks back to the UI thread.</param>
    /// <param name="time">Clock and timer factory; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="InvalidOperationException">The engine cannot report its activity.</exception>
    public SessionsMonitorViewModel(ConnectionSession session, IDialogService dialogs, IUiDispatcher dispatcher, TimeProvider? time = null)
        : this(session?.Profile ?? throw new ArgumentNullException(nameof(session)),
            session.Connection as IActivityReader ?? throw new InvalidOperationException(UiText.Get("activity.disabled.engine")),
            dialogs, dispatcher, time)
    {
        Session = session;
    }

    internal SessionsMonitorViewModel(ConnectionProfile profile, IActivityReader reader, IDialogService dialogs, IUiDispatcher dispatcher, TimeProvider? time)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(dispatcher);
        _profile = profile;
        _reader = reader;
        _dialogs = dialogs;
        _dispatcher = dispatcher;
        _time = time ?? TimeProvider.System;
        _timer = _time.CreateTimer(_ => _dispatcher.Post(Tick), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Raised when the user closes the tab.</summary>
    public event Action<SessionsMonitorViewModel>? CloseRequested;

    /// <summary>Raised by <see cref="CopyQueryCommand"/>; the head puts the text on the clipboard.</summary>
    public event Action<string>? CopyRequested;

    /// <summary>Gets the connection session the tab belongs to, for the status bar and the colour stripe.</summary>
    public ConnectionSession? Session { get; }

    /// <summary>Gets the tab title.</summary>
    public string Title { get; } = UiText.Get("activity.title");

    /// <summary>Gets the environment of the connection, which the head turns into the tab colour.</summary>
    public ConnectionEnvironment Environment => _profile.Environment;

    /// <summary>Tells whether a session can open a monitor, and says why not.</summary>
    /// <param name="session">A session, or <see langword="null"/> when not connected.</param>
    /// <returns>An empty text when it can, else the localized reason.</returns>
    public static string DisabledReason(ConnectionSession? session) =>
        session is null ? UiText.Get("activity.disabled.notConnected")
        : session.Connection is IActivityReader ? ""
        : UiText.Get("activity.disabled.engine");

    // ---- polling ----------------------------------------------------------------------------------------------------

    /// <summary>Gets or sets whether the user paused the refresh.</summary>
    [ObservableProperty]
    public partial bool IsPaused { get; set; }

    /// <summary>Gets or sets the message of the last failed read; the timer stays stopped until <see cref="TogglePauseCommand"/> resumes.</summary>
    [ObservableProperty]
    public partial string? Error { get; set; }

    /// <summary>Gets or sets the seconds between two reads (1, 2, 5 or 10); not persisted, a field of this view-model.</summary>
    [ObservableProperty]
    public partial int RefreshSeconds { get; set; } = 2;

    /// <summary>Gets the text of the refresh line: <c>Actualisé toutes les 2 s · il y a 1 s</c>, or the error.</summary>
    [ObservableProperty]
    public partial string RefreshText { get; private set; } = "";

    /// <summary>Gets the label of the pause button: <c>Pause</c>, or <c>Reprendre</c> when paused or failed.</summary>
    public string PauseLabel => UiText.Get(IsPaused || Error is not null ? "activity.resume" : "activity.pause");

    /// <summary>Gets a value indicating whether the timer may read: the tab is shown, not paused, no error.</summary>
    public bool IsPolling => _tabActive && !IsPaused && Error is null && !_disposed;

    partial void OnIsPausedChanged(bool value) => PollingChanged();

    partial void OnErrorChanged(string? value) => PollingChanged();

    partial void OnRefreshSecondsChanged(int value)
    {
        if (Array.IndexOf(Intervals, value) < 0)
        {
            RefreshSeconds = 2;
            return;
        }

        PollingChanged();
    }

    private void PollingChanged()
    {
        OnPropertyChanged(nameof(PauseLabel));
        OnPropertyChanged(nameof(IsPolling));
        var period = IsPolling ? TimeSpan.FromSeconds(RefreshSeconds) : Timeout.InfiniteTimeSpan;
        _timer.Change(period, period);
        UpdateRefreshText();
    }

    /// <summary>Tells the monitor whether its tab is the one on screen; it only polls while it is, and reads at once when shown.</summary>
    /// <param name="active"><see langword="true"/> when the tab is shown.</param>
    public void SetActive(bool active)
    {
        if (_tabActive == active || _disposed)
            return;
        _tabActive = active;
        PollingChanged();
        if (active && !IsPaused && Error is null)
            _ = RefreshAsync();
    }

    /// <summary>One timer tick: starts a read unless polling is off or the previous read has not finished.</summary>
    internal void Tick()
    {
        UpdateRefreshText();
        if (IsPolling && !_inFlight)
            _ = RefreshAsync();
    }

    /// <summary>Gets a task finishing when the read in progress (if any) has been applied or dropped.</summary>
    internal Task Idle => _current;

    /// <summary>Pauses the refresh, or resumes it (and reads at once) when paused or failed.</summary>
    [RelayCommand]
    private void TogglePause()
    {
        if (IsPaused || Error is not null)
        {
            Error = null;
            IsPaused = false;
            _ = RefreshAsync();
        }
        else
            IsPaused = true;
    }

    /// <summary>Reads the activity now, even while paused; a read still in flight is abandoned and its result dropped.</summary>
    /// <returns>A task finishing when the result has been applied (or dropped).</returns>
    [RelayCommand]
    internal Task RefreshAsync() => _current = ReadAsync();

    private async Task ReadAsync()
    {
        if (_disposed)
            return;
        _readCts?.Cancel();
        var cts = _readCts = new CancellationTokenSource();
        var mine = ++_sequence;
        _inFlight = true;
        var started = _time.GetTimestamp();
        try
        {
            var snapshot = await Task.Run(() => _reader.ReadAsync(cts.Token).AsTask(), cts.Token);
            if (mine != _sequence || _disposed)
                return; // a newer read superseded this one: never let the older picture replace it
            Apply(snapshot, _time.GetElapsedTime(started));
        }
        catch (OperationCanceledException)
        {
            // abandoned by a newer read or by Dispose
        }
        catch (Exception ex)
        {
            if (mine == _sequence && !_disposed)
                Error = UiText.Format("activity.error", ex.Message);
        }
        finally
        {
            if (mine == _sequence)
                _inFlight = false;
        }
    }

    private void UpdateRefreshText() =>
        RefreshText = Error is not null ? Error
            : IsPaused ? UiText.Get("activity.paused")
            : UiText.Format("activity.refreshed", RefreshSeconds, Age());

    private string Age()
    {
        if (_appliedAt is not { } at)
            return "…";
        var seconds = (int)Math.Max(0, (_time.GetUtcNow() - at).TotalSeconds);
        return seconds == 0 ? UiText.Get("activity.age.now")
            : seconds < 60 ? UiText.Format("activity.age.seconds", seconds)
            : UiText.Format("activity.age.minutes", seconds / 60);
    }

    // ---- snapshot -> state ------------------------------------------------------------------------------------------

    /// <summary>Gets a value indicating whether a snapshot was read.</summary>
    [ObservableProperty]
    public partial bool HasData { get; private set; }

    /// <summary>Gets the status bar text: <c>pg_stat_activity · 47 lignes · 6 ms</c>.</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = "";

    /// <summary>Gets the tile text <c>n / max</c> (just <c>n</c> when the limit is unknown).</summary>
    [ObservableProperty]
    public partial string ConnectionsText { get; private set; } = "";

    /// <summary>Gets how full the connection limit is, 0 to 1; 0 when the limit is unknown.</summary>
    [ObservableProperty]
    public partial double ConnectionsRatio { get; private set; }

    /// <summary>Gets a value indicating whether the transactions tile is shown (the engine has a counter).</summary>
    [ObservableProperty]
    public partial bool HasTransactionsTile { get; private set; }

    /// <summary>Gets the transactions per second between the last two reads, or <see langword="null"/> before the second one.</summary>
    [ObservableProperty]
    public partial double? TransactionsPerSecond { get; private set; }

    /// <summary>Gets the last 60 transaction rates, oldest first, for the sparkline.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<double> TransactionsHistory { get; private set; } = [];

    /// <summary>Gets how many running statements last longer than 30 s.</summary>
    [ObservableProperty]
    public partial int LongQueryCount { get; private set; }

    /// <summary>Gets how many running statements last longer than 5 min.</summary>
    [ObservableProperty]
    public partial int VeryLongQueryCount { get; private set; }

    /// <summary>Gets how many sessions wait for a lock.</summary>
    [ObservableProperty]
    public partial int WaitingLockCount { get; private set; }

    /// <summary>Gets the session holding the most blocked sessions, or <see langword="null"/> when none is blocked.</summary>
    [ObservableProperty]
    public partial string? TopBlockerId { get; private set; }

    /// <summary>Gets a value indicating whether the locks tile has the danger styling (someone is blocked).</summary>
    public bool LocksAreDanger => WaitingLockCount > 0;

    /// <summary>Gets the blocking chains, one per root blocker.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<BlockingChainNode> Chains { get; private set; } = [];

    /// <summary>Gets a value indicating whether the engine could not tell who blocks whom with these rights.</summary>
    [ObservableProperty]
    public partial bool BlockingUnavailable { get; private set; }

    /// <summary>Gets the heading of the chains panel: <c>1 chaîne de blocage</c>, or the unavailable text.</summary>
    [ObservableProperty]
    public partial string ChainsSummary { get; private set; } = "";

    private void Apply(ActivitySnapshot snapshot, TimeSpan readTime)
    {
        _snapshot = snapshot;
        _appliedAt = _time.GetUtcNow();
        HasData = true;
        ConnectionsText = snapshot.MaxConnections is { } max
            ? $"{snapshot.ActiveConnections} / {max}"
            : snapshot.ActiveConnections.ToString(CultureInfo.InvariantCulture);
        ConnectionsRatio = snapshot.MaxConnections is > 0 and var m ? Math.Clamp((double)snapshot.ActiveConnections / m, 0, 1) : 0;
        UpdateRate(snapshot);

        var running = snapshot.Sessions.Where(s => s.State == SessionState.Active && s.Duration is not null).ToList();
        LongQueryCount = running.Count(s => s.Duration > LongQuery);
        VeryLongQueryCount = running.Count(s => s.Duration > VeryLongQuery);
        WaitingLockCount = snapshot.Blocking.Select(e => e.BlockedId).Distinct().Count();
        TopBlockerId = snapshot.Blocking.GroupBy(e => e.BlockerId)
            .Select(g => (Id: g.Key, Count: g.Select(e => e.BlockedId).Distinct().Count()))
            .OrderByDescending(x => x.Count).ThenBy(x => x.Id, StringComparer.Ordinal)
            .Select(x => x.Id).FirstOrDefault();
        OnPropertyChanged(nameof(LocksAreDanger));

        Chains = BlockingChains.Build(snapshot.Blocking);
        BlockingUnavailable = snapshot.BlockingUnavailable;
        ChainsSummary = snapshot.BlockingUnavailable ? UiText.Get("activity.chains.unavailable")
            : Chains.Count == 0 ? UiText.Get("activity.chains.none")
            : UiText.FormatPlural("activity.chains.count", Chains.Count);
        StatusText = UiText.FormatPlural("activity.status", snapshot.Sessions.Count, snapshot.SourceName, (int)readTime.TotalMilliseconds);
        Project();
        UpdateRefreshText();
    }

    private void UpdateRate(ActivitySnapshot snapshot)
    {
        HasTransactionsTile = snapshot.TransactionCounter is not null;
        if (snapshot.TransactionCounter is not { } counter)
        {
            _previousCounter = null;
            _history.Clear();
            TransactionsPerSecond = null;
            TransactionsHistory = [];
            return;
        }

        var elapsed = (snapshot.TakenAt - _previousAt).TotalSeconds;
        if (_previousCounter is { } before && elapsed > 0 && counter >= before) // a smaller counter means the server restarted
        {
            var rate = (counter - before) / elapsed;
            TransactionsPerSecond = rate;
            _history.Add(rate);
            if (_history.Count > HistoryLength)
                _history.RemoveAt(0);
            TransactionsHistory = [.. _history];
        }

        _previousCounter = counter;
        _previousAt = snapshot.TakenAt;
    }

    // ---- filter, sort, rows -----------------------------------------------------------------------------------------

    /// <summary>Gets or sets the filter tab.</summary>
    [ObservableProperty]
    public partial SessionFilter Filter { get; set; }

    /// <summary>Gets or sets whether server background processes are hidden.</summary>
    [ObservableProperty]
    public partial bool HideSystem { get; set; } = true;

    /// <summary>Gets or sets the text matched against user, application and query, ignoring case.</summary>
    [ObservableProperty]
    public partial string FilterText { get; set; } = "";

    /// <summary>Gets the column the table is sorted by.</summary>
    [ObservableProperty]
    public partial SessionSortColumn SortColumn { get; private set; } = SessionSortColumn.Duration;

    /// <summary>Gets a value indicating whether the sort is descending.</summary>
    public bool SortDescending => _sortDescending;

    /// <summary>Gets the rows the table shows.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<SessionRowViewModel> Rows { get; private set; } = [];

    /// <summary>Gets the text of the <c>Toutes n</c> filter tab.</summary>
    public string AllTabText => UiText.Format("activity.filter.all", _counts[0]);

    /// <summary>Gets the text of the <c>Actives n</c> filter tab.</summary>
    public string ActiveTabText => UiText.Format("activity.filter.active", _counts[1]);

    /// <summary>Gets the text of the <c>Transaction inactive n</c> filter tab.</summary>
    public string IdleInTransactionTabText => UiText.Format("activity.filter.idleInTransaction", _counts[2]);

    /// <summary>Gets the text of the <c>En attente n</c> filter tab.</summary>
    public string WaitingTabText => UiText.Format("activity.filter.waiting", _counts[3]);

    partial void OnFilterChanged(SessionFilter value) => Project();

    partial void OnHideSystemChanged(bool value) => Project();

    partial void OnFilterTextChanged(string value) => Project();

    /// <summary>Sorts by a column; choosing the sorted column again reverses the order.</summary>
    /// <param name="column">The column header that was clicked.</param>
    public void SortBy(SessionSortColumn column)
    {
        if (column == SortColumn)
            _sortDescending = !_sortDescending;
        else
        {
            SortColumn = column;
            _sortDescending = column == SessionSortColumn.Duration;
        }

        OnPropertyChanged(nameof(SortDescending));
        Project();
    }

    private void Project()
    {
        if (_snapshot is not { } snapshot)
            return;
        var blockedBy = new Dictionary<string, int>();
        foreach (var chain in Chains)
            Collect(chain, blockedBy);
        var blocked = snapshot.Blocking.Select(e => e.BlockedId).ToHashSet();
        var all = snapshot.Sessions.Where(s => !HideSystem || !s.IsSystem)
            .Select(s => new SessionRowViewModel(s, blockedBy.GetValueOrDefault(s.Id), blocked.Contains(s.Id))).ToList();
        _counts = [all.Count, CountOf(all, SessionState.Active), CountOf(all, SessionState.IdleInTransaction), CountOf(all, SessionState.Waiting)];
        OnPropertyChanged(nameof(AllTabText));
        OnPropertyChanged(nameof(ActiveTabText));
        OnPropertyChanged(nameof(IdleInTransactionTabText));
        OnPropertyChanged(nameof(WaitingTabText));

        IEnumerable<SessionRowViewModel> shown = Filter switch
        {
            SessionFilter.Active => all.Where(r => r.Session.State == SessionState.Active),
            SessionFilter.IdleInTransaction => all.Where(r => r.Session.State == SessionState.IdleInTransaction),
            SessionFilter.Waiting => all.Where(r => r.Session.State == SessionState.Waiting),
            _ => all,
        };
        var text = FilterText.Trim();
        if (text.Length > 0)
            shown = shown.Where(r => Contains(r.User, text) || Contains(r.Application, text) || Contains(r.Session.Query, text));
        _rowsById = all.ToDictionary(r => r.Id);
        Rows = Sort(shown).ToList();
        ResolveSelection();
    }

    private static int CountOf(List<SessionRowViewModel> rows, SessionState state) => rows.Count(r => r.Session.State == state);

    private static bool Contains(string? value, string text) => value?.Contains(text, StringComparison.OrdinalIgnoreCase) == true;

    private static void Collect(BlockingChainNode node, Dictionary<string, int> into)
    {
        into[node.SessionId] = node.BlockedCount;
        foreach (var child in node.Children)
            Collect(child, into);
    }

    private IOrderedEnumerable<SessionRowViewModel> Sort(IEnumerable<SessionRowViewModel> rows)
    {
        Func<SessionRowViewModel, object?> key = SortColumn switch
        {
            SessionSortColumn.Pid => r => long.TryParse(r.Id, CultureInfo.InvariantCulture, out var n) ? n : long.MaxValue,
            SessionSortColumn.User => r => r.User,
            SessionSortColumn.Application => r => r.Application,
            SessionSortColumn.Client => r => r.Client,
            SessionSortColumn.State => r => r.StateText,
            SessionSortColumn.Wait => r => r.WaitText,
            SessionSortColumn.Query => r => r.QueryLine,
            _ => r => r.Session.Duration ?? TimeSpan.MinValue,
        };
        var comparer = Comparer<object?>.Create((a, b) =>
            a is string x && b is string y ? string.Compare(x, y, StringComparison.OrdinalIgnoreCase) : Comparer<object?>.Default.Compare(a, b));
        return _sortDescending ? rows.OrderByDescending(key, comparer) : rows.OrderBy(key, comparer);
    }

    // ---- selection and detail ---------------------------------------------------------------------------------------

    /// <summary>Gets or sets the selected row; it is kept across refreshes by session id.</summary>
    [ObservableProperty]
    public partial SessionRowViewModel? SelectedSession { get; set; }

    /// <summary>Gets the detail pane of the selected session, or <see langword="null"/>.</summary>
    [ObservableProperty]
    public partial SessionDetailViewModel? Detail { get; private set; }

    /// <summary>Gets the message shown in the detail pane for one refresh when the selected session has gone.</summary>
    [ObservableProperty]
    public partial string? SelectionGoneText { get; private set; }

    partial void OnSelectedSessionChanged(SessionRowViewModel? value)
    {
        _selectedId = value?.Id;
        SelectionGoneText = null;
        Detail = value is null ? null : BuildDetail(value);
        CancelQueryCommand.NotifyCanExecuteChanged();
        TerminateSessionCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Selects a session by id, for a click in the blocking chains panel.</summary>
    /// <param name="id">The session id.</param>
    public void SelectSession(string id)
    {
        if (_rowsById.TryGetValue(id, out var row))
            SelectedSession = row;
    }

    private void ResolveSelection()
    {
        if (_selectedId is not { } id)
        {
            SelectionGoneText = null; // the notice lasts one refresh
            return;
        }

        if (!_rowsById.TryGetValue(id, out var fresh))
        {
            // The session has ended: say so for this refresh, then forget it.
            SelectedSession = null;
            SelectionGoneText = UiText.Format("activity.selectionGone", id);
            return;
        }

        SelectedSession = fresh; // same id, fresh data (a row hidden by a filter stays selected)
    }

    private SessionDetailViewModel BuildDetail(SessionRowViewModel row)
    {
        var s = row.Session;
        List<DetailField> fields = [];
        void Add(string key, string? value)
        {
            if (!string.IsNullOrEmpty(value))
                fields.Add(new DetailField(UiText.Get(key), value));
        }

        Add("activity.field.user", s.User);
        Add("activity.field.application", s.Application);
        Add("activity.field.client", s.Client);
        Add("activity.field.transactionStart", s.TransactionStart?.ToLocalTime().ToString("T", UiText.Culture));
        Add("activity.field.lastState", s.StateChange is { } at ? AgeOf(at) : null);
        Add("activity.field.locksHeld", s.LocksHeld?.ToString(CultureInfo.InvariantCulture));
        var note = s.State == SessionState.IdleInTransaction && row.BlockedCount > 0
            ? UiText.FormatPlural("activity.note.idleBlocking", row.BlockedCount, row.DurationText)
            : null;
        return new SessionDetailViewModel(row, fields, note, _reader.SupportsCancelQuery);
    }

    private string AgeOf(DateTimeOffset at)
    {
        var seconds = (int)Math.Max(0, (_time.GetUtcNow() - at).TotalSeconds);
        return seconds < 60 ? UiText.Format("activity.age.seconds", seconds) : UiText.Format("activity.age.minutes", seconds / 60);
    }

    /// <summary>Gets a value indicating whether the engine can cancel a statement (the head hides the command otherwise).</summary>
    public bool SupportsCancelQuery => _reader.SupportsCancelQuery;

    /// <summary>Gets a value indicating whether cancel and terminate ask the production confirmation.</summary>
    public bool IsProduction => _profile.Environment == ConnectionEnvironment.Production;

    /// <summary>Gets the line under the action buttons on a production connection, empty elsewhere.</summary>
    public string ProductionHint => IsProduction ? UiText.Get("activity.production") : "";

    /// <summary>Puts the last statement of the selected session on the clipboard.</summary>
    [RelayCommand]
    private void CopyQuery()
    {
        if (Detail is { Query.Length: > 0 } d)
            CopyRequested?.Invoke(d.Query);
    }

    // ---- actions ----------------------------------------------------------------------------------------------------

    private bool CanCancelQuery() => SelectedSession is not null && _reader.SupportsCancelQuery;

    private bool CanTerminate() => SelectedSession is not null;

    /// <summary>Cancels the statement the selected session is running, after a confirmation.</summary>
    /// <returns>A task finishing when the action and the refresh that follows are done.</returns>
    [RelayCommand(CanExecute = nameof(CanCancelQuery))]
    private Task CancelQueryAsync() => ActAsync(terminate: false);

    /// <summary>Ends the selected session and rolls back its open transaction, after a confirmation.</summary>
    /// <returns>A task finishing when the action and the refresh that follows are done.</returns>
    [RelayCommand(CanExecute = nameof(CanTerminate))]
    private Task TerminateSessionAsync() => ActAsync(terminate: true);

    private async Task ActAsync(bool terminate)
    {
        if (SelectedSession is not { } row || (!terminate && !_reader.SupportsCancelQuery))
            return;
        var who = UiText.Format("activity.confirm.who", row.Id, row.User.Length > 0 ? row.User : "?");
        var key = terminate ? "activity.confirm.terminate" : "activity.confirm.cancel";
        var title = UiText.Get(key + ".title");
        var message = UiText.Format(key + ".message", who);
        bool confirmed;
        if (IsProduction)
        {
            // A read-only monitor, but ending a session is a modifying act: typed word for terminate, plain confirmation for cancel.
            var statement = new ConfirmedStatement(UiText.Format(key + ".statement", row.Id), null, StatementKind.Other);
            var request = new ProductionConfirmationRequest(
                ConfirmationKind.Run, _profile.Name, _profile.Database ?? "", [statement], null,
                terminate ? ProductionGuard.RequiredText(_profile) : null, false, false, title, message, UiText.Get(key + ".button"));
            confirmed = (await _dialogs.ConfirmProductionAsync(request)).Confirmed;
        }
        else
            confirmed = await _dialogs.ConfirmAsync(title, message);
        if (!confirmed)
            return;
        try
        {
            if (terminate)
                await _reader.TerminateSessionAsync(row.Id, CancellationToken.None);
            else
                await _reader.CancelQueryAsync(row.Id, CancellationToken.None);
        }
        catch (Exception ex) when (ex is ActivityActionFailedException or ArgumentException or NotSupportedException)
        {
            await _dialogs.ShowErrorAsync(title, ex.Message);
        }

        await RefreshAsync();
    }

    /// <summary>Asks the workspace to close the tab.</summary>
    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this);

    /// <summary>Stops the timer and abandons a read in flight.</summary>
    public void Dispose()
    {
        _disposed = true;
        _timer.Dispose();
        _readCts?.Cancel();
    }
}
