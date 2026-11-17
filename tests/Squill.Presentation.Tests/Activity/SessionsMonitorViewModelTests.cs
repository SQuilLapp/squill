using Squill.Core;
using Squill.Core.Activity;
using Squill.Data;
using Squill.Presentation.Activity;
using Squill.Presentation.Localization;
using Xunit;

namespace Squill.Presentation.Tests.Activity;

public sealed class SessionsMonitorViewModelTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    private readonly ManualTime _time = new(T0);
    private readonly FakeReader _reader = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly List<SessionsMonitorViewModel> _created = [];

    public void Dispose()
    {
        foreach (var vm in _created)
            vm.Dispose();
    }

    private SessionsMonitorViewModel Create(ConnectionEnvironment environment = ConnectionEnvironment.None, bool active = false)
    {
        var profile = new ConnectionProfile(Guid.NewGuid(), "main", DatabaseDialect.PostgreSql, "h", 1, "appdb", "u", null, null, environment);
        var vm = new SessionsMonitorViewModel(profile, _reader, _dialogs, new ImmediateDispatcher(), _time);
        _created.Add(vm);
        if (active)
            vm.SetActive(true);
        return vm;
    }

    private static ServerSession Session(string id, SessionState state = SessionState.Active, double? seconds = null, string? user = "app", string? query = "select 1", bool system = false) =>
        new(id, user, "psql", "10.0.0.1:5000", state, state.ToString(), seconds is { } s ? TimeSpan.FromSeconds(s) : null, null, query, null, null, null, system);

    private ActivitySnapshot Snapshot(IReadOnlyList<ServerSession> sessions, IReadOnlyList<BlockingEdge>? blocking = null, long? counter = null, DateTimeOffset? at = null) =>
        new(at ?? _time.GetUtcNow(), sessions.Count, 100, counter, sessions, blocking ?? [], SourceName: "pg_stat_activity");

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 400 && !condition(); i++)
            await Task.Delay(5);
        Assert.True(condition());
    }

    private async Task LoadAsync(SessionsMonitorViewModel vm, ActivitySnapshot snapshot)
    {
        _reader.Auto = snapshot;
        await vm.RefreshAsync();
    }

    // ---- chains -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Chains_TwoBlockedBehindOne()
    {
        var chains = BlockingChains.Build([new("1", "2", null, null), new("1", "3", null, null)]);
        var root = Assert.Single(chains);
        Assert.Equal("1", root.SessionId);
        Assert.Equal(["2", "3"], root.Children.Select(c => c.SessionId));
        Assert.Equal(2, root.BlockedCount);
    }

    [Fact]
    public void Chains_ChainOfThree()
    {
        var chains = BlockingChains.Build([new("2", "3", null, null), new("1", "2", null, null)]);
        var root = Assert.Single(chains);
        Assert.Equal("1", root.SessionId);
        Assert.Equal(2, root.BlockedCount);
        var middle = Assert.Single(root.Children);
        Assert.Equal("2", middle.SessionId);
        Assert.Equal(1, middle.BlockedCount);
        Assert.Equal("3", Assert.Single(middle.Children).SessionId);
    }

    [Fact]
    public void Chains_CycleDoesNotLoop()
    {
        var chains = BlockingChains.Build([new("1", "2", null, null), new("2", "3", null, null), new("3", "1", null, null)]);
        var root = Assert.Single(chains);
        Assert.Equal("1", root.SessionId); // broken at the smallest id
        Assert.Equal(2, root.BlockedCount);
        Assert.Empty(root.Children[0].Children[0].Children); // 3 does not lead back to 1
    }

    [Fact]
    public void Chains_SeveralRoots_AndNoEdges()
    {
        Assert.Empty(BlockingChains.Build([]));
        var chains = BlockingChains.Build([new("9", "1", null, null), new("5", "2", null, null)]);
        Assert.Equal(["5", "9"], chains.Select(c => c.SessionId));
    }

    // ---- figures ----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Tiles_ShowTheFigures()
    {
        var vm = Create();
        var snapshot = Snapshot(
            [Session("10", seconds: 5), Session("11", seconds: 40), Session("12", seconds: 400), Session("13", SessionState.Waiting), Session("14", SessionState.Idle, seconds: 900)],
            [new("100", "13", "Lock", "t"), new("100", "14", null, null), new("101", "12", null, null)]);
        await LoadAsync(vm, snapshot with { ActiveConnections = 25 });

        Assert.Equal("25 / 100", vm.ConnectionsText);
        Assert.Equal(0.25, vm.ConnectionsRatio);
        Assert.Equal(2, vm.LongQueryCount); // 40 s and 400 s; the idle session does not count
        Assert.Equal(1, vm.VeryLongQueryCount);
        Assert.Equal(3, vm.WaitingLockCount);
        Assert.Equal("100", vm.TopBlockerId);
        Assert.True(vm.LocksAreDanger);
        Assert.False(vm.HasTransactionsTile);
        Assert.Contains("pg_stat_activity", vm.StatusText);
        Assert.Equal(["100", "101"], vm.Chains.Select(c => c.SessionId));
    }

    [Fact]
    public async Task Tiles_NoBlocking_NoDanger()
    {
        var vm = Create();
        await LoadAsync(vm, Snapshot([Session("1")]));
        Assert.Equal(0, vm.WaitingLockCount);
        Assert.Null(vm.TopBlockerId);
        Assert.False(vm.LocksAreDanger);
        Assert.Empty(vm.Chains);
    }

    [Fact]
    public async Task BlockingUnavailable_IsReported()
    {
        var vm = Create();
        await LoadAsync(vm, Snapshot([Session("1")]) with { BlockingUnavailable = true });
        Assert.True(vm.BlockingUnavailable);
        Assert.Equal(UiText.Get("activity.chains.unavailable"), vm.ChainsSummary);
    }

    [Fact]
    public async Task TransactionsPerSecond_FromTwoSamples()
    {
        var vm = Create();
        await LoadAsync(vm, Snapshot([Session("1")], counter: 100, at: T0));
        Assert.True(vm.HasTransactionsTile);
        Assert.Null(vm.TransactionsPerSecond); // one sample is not a rate
        await LoadAsync(vm, Snapshot([Session("1")], counter: 160, at: T0.AddSeconds(2)));
        Assert.Equal(30, vm.TransactionsPerSecond);
        Assert.Equal([30.0], vm.TransactionsHistory);
        await LoadAsync(vm, Snapshot([Session("1")], counter: 10, at: T0.AddSeconds(4))); // server restarted: no negative rate
        Assert.Equal([30.0], vm.TransactionsHistory);
    }

    [Fact]
    public async Task TransactionsHistory_KeepsTheLastSixty()
    {
        var vm = Create();
        for (var i = 0; i < 70; i++)
            await LoadAsync(vm, Snapshot([Session("1")], counter: i * 10, at: T0.AddSeconds(i)));
        Assert.Equal(60, vm.TransactionsHistory.Count);
    }

    [Theory]
    [InlineData(null, "-")]
    [InlineData(0.4, "400 ms")]
    [InlineData(48.0, "48 s")]
    [InlineData(372.0, "6 min 12 s")]
    public void DurationText(double? seconds, string expected) =>
        Assert.Equal(expected, SessionRowViewModel.FormatDuration(seconds is { } s ? TimeSpan.FromSeconds(s) : null));

    [Fact]
    public async Task Row_DurationSeverityAndStateDot()
    {
        var vm = Create();
        await LoadAsync(vm, Snapshot([Session("1", seconds: 10), Session("2", seconds: 40), Session("3", seconds: 400), Session("4", SessionState.IdleInTransaction, 400), Session("5", SessionState.Idle)]));
        var rows = vm.Rows.ToDictionary(r => r.Id);
        Assert.Equal(ActivitySeverity.Normal, rows["1"].DurationSeverity);
        Assert.Equal(ActivitySeverity.Warning, rows["2"].DurationSeverity);
        Assert.Equal(ActivitySeverity.Danger, rows["3"].DurationSeverity);
        Assert.Equal(ActivitySeverity.Normal, rows["4"].DurationSeverity);
        Assert.Equal(StateDotKind.Success, rows["1"].StateDot);
        Assert.Equal(StateDotKind.Warning, rows["4"].StateDot);
        Assert.Equal(StateDotKind.Muted, rows["5"].StateDot);
    }

    // ---- filters and sort -------------------------------------------------------------------------------------------

    [Fact]
    public async Task Filters_AndCounts()
    {
        var vm = Create();
        await LoadAsync(vm, Snapshot([
            Session("1", seconds: 1), Session("2", SessionState.IdleInTransaction, 5), Session("3", SessionState.Waiting, 9),
            Session("4", SessionState.Idle), Session("5", system: true), Session("6", user: "Alice", query: "update Orders")]));

        Assert.Equal(5, vm.Rows.Count); // the system process is hidden by default
        Assert.Equal(UiText.Format("activity.filter.all", 5), vm.AllTabText);
        Assert.Equal(UiText.Format("activity.filter.active", 2), vm.ActiveTabText);
        Assert.Equal(UiText.Format("activity.filter.idleInTransaction", 1), vm.IdleInTransactionTabText);
        Assert.Equal(UiText.Format("activity.filter.waiting", 1), vm.WaitingTabText);

        vm.Filter = SessionFilter.IdleInTransaction;
        Assert.Equal(["2"], vm.Rows.Select(r => r.Id));
        vm.Filter = SessionFilter.All;
        vm.HideSystem = false;
        Assert.Equal(6, vm.Rows.Count);
        vm.FilterText = "ALICE";
        Assert.Equal(["6"], vm.Rows.Select(r => r.Id));
        vm.FilterText = "orders"; // matches the query too
        Assert.Equal(["6"], vm.Rows.Select(r => r.Id));
    }

    [Fact]
    public async Task Sort_DefaultsToDurationDescending_ClickChangesIt()
    {
        var vm = Create();
        await LoadAsync(vm, Snapshot([Session("1", seconds: 5), Session("2", seconds: 50), Session("3"), Session("10", seconds: 7)]));
        Assert.Equal(["2", "10", "1", "3"], vm.Rows.Select(r => r.Id));
        vm.SortBy(SessionSortColumn.Duration);
        Assert.Equal(["3", "1", "10", "2"], vm.Rows.Select(r => r.Id));
        vm.SortBy(SessionSortColumn.Pid);
        Assert.Equal(["1", "2", "3", "10"], vm.Rows.Select(r => r.Id)); // numeric, not alphabetical
    }

    // ---- selection and detail ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Selection_IsKeptAcrossRefreshes_AndClearedWhenTheSessionEnds()
    {
        var vm = Create();
        await LoadAsync(vm, Snapshot([Session("1", seconds: 5), Session("2", seconds: 9)]));
        vm.SelectedSession = vm.Rows.Single(r => r.Id == "1");

        await LoadAsync(vm, Snapshot([Session("1", seconds: 15), Session("2", seconds: 19)]));
        Assert.Equal("1", vm.SelectedSession?.Id);
        Assert.Equal("15 s", vm.SelectedSession!.DurationText); // fresh data on the kept selection
        Assert.Equal("1", vm.Detail?.Row.Id);
        Assert.Null(vm.SelectionGoneText);

        await LoadAsync(vm, Snapshot([Session("2", seconds: 29)]));
        Assert.Null(vm.SelectedSession);
        Assert.Null(vm.Detail);
        Assert.Equal(UiText.Format("activity.selectionGone", "1"), vm.SelectionGoneText);

        await LoadAsync(vm, Snapshot([Session("2", seconds: 39)]));
        Assert.Null(vm.SelectionGoneText); // said for one refresh only
    }

    [Fact]
    public async Task Detail_ShowsOnlyWhatTheEngineProvided_AndExplainsAnIdleBlocker()
    {
        var vm = Create();
        var idle = new ServerSession("7", "bob", null, null, SessionState.IdleInTransaction, "idle in transaction", TimeSpan.FromSeconds(372), null, "update t set a = 1", T0.AddMinutes(-6), null, 3, false);
        await LoadAsync(vm, Snapshot([idle, Session("8", SessionState.Waiting), Session("9", SessionState.Waiting)], [new("7", "8", null, null), new("7", "9", null, null)]));
        vm.SelectSession("7");

        var detail = Assert.IsType<SessionDetailViewModel>(vm.Detail);
        Assert.Equal([UiText.Get("activity.field.user"), UiText.Get("activity.field.transactionStart"), UiText.Get("activity.field.locksHeld")], detail.Fields.Select(f => f.Label));
        Assert.Equal("update t set a = 1", detail.Query);
        Assert.Equal(UiText.FormatPlural("activity.note.idleBlocking", 2, "6 min 12 s"), detail.Note);
        vm.SelectSession("8");
        Assert.Null(vm.Detail!.Note);
    }

    [Fact]
    public async Task CopyQuery_RaisesTheText()
    {
        var vm = Create();
        string? copied = null;
        vm.CopyRequested += t => copied = t;
        await LoadAsync(vm, Snapshot([Session("1", query: "select 42")]));
        vm.SelectSession("1");
        vm.CopyQueryCommand.Execute(null);
        Assert.Equal("select 42", copied);
    }

    // ---- polling ----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task InactiveTab_DoesNotPoll_AndShowingItReadsAtOnce()
    {
        var vm = Create();
        _reader.Auto = Snapshot([Session("1")]);
        vm.Tick();
        vm.Tick();
        await vm.Idle;
        Assert.Equal(0, _reader.Reads);
        Assert.Equal(Timeout.InfiniteTimeSpan, _time.Timer.Period);

        vm.SetActive(true);
        await vm.Idle;
        Assert.Equal(1, _reader.Reads);
        Assert.Equal(TimeSpan.FromSeconds(2), _time.Timer.Period);

        vm.SetActive(false);
        Assert.Equal(Timeout.InfiniteTimeSpan, _time.Timer.Period);
        vm.Tick();
        await vm.Idle;
        Assert.Equal(1, _reader.Reads); // hidden again: no polling in the background
    }

    [Fact]
    public async Task PausedMonitor_DoesNotPoll_AndResumeReadsAtOnce()
    {
        _reader.Auto = Snapshot([Session("1")]);
        var vm = Create(active: true);
        await vm.Idle;
        vm.TogglePauseCommand.Execute(null);
        Assert.True(vm.IsPaused);
        Assert.Equal(UiText.Get("activity.resume"), vm.PauseLabel);
        vm.Tick();
        await vm.Idle;
        Assert.Equal(1, _reader.Reads);

        vm.TogglePauseCommand.Execute(null);
        await vm.Idle;
        Assert.Equal(2, _reader.Reads);
    }

    [Fact]
    public async Task ATickWhileAReadIsInFlight_IsSkipped()
    {
        var vm = Create(active: true);
        await WaitUntilAsync(() => _reader.Calls.Count == 1); // the read started when the tab was shown and has not answered
        vm.Tick();
        vm.Tick();
        vm.Tick();
        _reader.Complete(0, Snapshot([Session("1")]));
        await vm.Idle;
        Assert.Equal(1, _reader.Reads);
        Assert.Single(vm.Rows);

        vm.Tick(); // the read is done: the next tick reads again
        await WaitUntilAsync(() => _reader.Calls.Count == 2);
    }

    [Fact]
    public async Task AStaleRead_NeverReplacesANewerOne()
    {
        var vm = Create();
        var first = vm.RefreshAsync();
        await WaitUntilAsync(() => _reader.Calls.Count == 1);
        var second = vm.RefreshAsync(); // a manual refresh while the first is slow
        await WaitUntilAsync(() => _reader.Calls.Count == 2);

        _reader.Complete(1, Snapshot([Session("new")]));
        await second;
        _reader.Complete(0, Snapshot([Session("old")]));
        await first;

        Assert.Equal(["new"], vm.Rows.Select(r => r.Id));
    }

    [Fact]
    public async Task AReadError_StopsTheTimer_ShowsTheMessage_AndResumeReadsAgain()
    {
        var vm = Create(active: true);
        await WaitUntilAsync(() => _reader.Calls.Count == 1);
        _reader.Fail(0, new ActivityUnavailableException("activity.permission", "no right"));
        await vm.Idle;

        Assert.Contains("no right", vm.Error);
        Assert.Equal(vm.Error, vm.RefreshText);
        Assert.False(vm.IsPolling);
        Assert.Equal(Timeout.InfiniteTimeSpan, _time.Timer.Period);
        vm.Tick();
        await vm.Idle;
        Assert.Equal(1, _reader.Reads);
        Assert.Equal(UiText.Get("activity.resume"), vm.PauseLabel);

        _reader.Auto = Snapshot([Session("1")]);
        vm.TogglePauseCommand.Execute(null);
        await vm.Idle;
        Assert.Null(vm.Error);
        Assert.True(vm.IsPolling);
        Assert.Single(vm.Rows);
    }

    [Fact]
    public async Task Dispose_StopsPolling_AndDropsAReadInFlight()
    {
        var vm = Create(active: true);
        await WaitUntilAsync(() => _reader.Calls.Count == 1);
        vm.Dispose();
        _reader.Complete(0, Snapshot([Session("1")]));
        await vm.Idle;
        Assert.Empty(vm.Rows);
        vm.Tick();
        await vm.Idle;
        Assert.Equal(1, _reader.Reads);
    }

    [Fact]
    public void RefreshSeconds_OnlyTakesTheOfferedValues()
    {
        var vm = Create(active: true);
        vm.RefreshSeconds = 5;
        Assert.Equal(TimeSpan.FromSeconds(5), _time.Timer.Period);
        vm.RefreshSeconds = 3;
        Assert.Equal(2, vm.RefreshSeconds);
    }

    [Fact]
    public async Task RefreshText_SaysTheAge()
    {
        var vm = Create();
        await LoadAsync(vm, Snapshot([Session("1")]));
        Assert.Equal(UiText.Format("activity.refreshed", 2, UiText.Get("activity.age.now")), vm.RefreshText);
        _time.Now = _time.Now.AddSeconds(1);
        vm.Tick();
        Assert.Equal(UiText.Format("activity.refreshed", 2, UiText.Format("activity.age.seconds", 1)), vm.RefreshText);
    }

    // ---- actions ----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Cancel_Unsupported_HidesTheCommand()
    {
        _reader.SupportsCancelQuery = false;
        var vm = Create();
        await LoadAsync(vm, Snapshot([Session("1")]));
        vm.SelectSession("1");

        Assert.False(vm.SupportsCancelQuery);
        Assert.False(vm.Detail!.CanCancel);
        Assert.False(vm.CancelQueryCommand.CanExecute(null));
        Assert.True(vm.TerminateSessionCommand.CanExecute(null));
    }

    [Fact]
    public async Task Cancel_AsksAPlainConfirmation_ThenCancels_AndRefreshes()
    {
        var vm = Create();
        await LoadAsync(vm, Snapshot([Session("1")]));
        vm.SelectSession("1");
        var reads = _reader.Reads;

        await vm.CancelQueryCommand.ExecuteAsync(null);

        Assert.Single(_dialogs.Confirms);
        Assert.Empty(_dialogs.ProductionRequests);
        Assert.Equal(["1"], _reader.Cancelled);
        Assert.Equal(reads + 1, _reader.Reads);
    }

    [Fact]
    public async Task Terminate_Refused_DoesNothing()
    {
        var vm = Create();
        await LoadAsync(vm, Snapshot([Session("1")]));
        vm.SelectSession("1");
        _dialogs.ConfirmResult = false;

        await vm.TerminateSessionCommand.ExecuteAsync(null);

        Assert.Empty(_reader.Terminated);
        Assert.Single(_dialogs.Confirms);
    }

    [Fact]
    public async Task Terminate_OnProduction_AsksTheTypedConfirmation()
    {
        var vm = Create(ConnectionEnvironment.Production);
        await LoadAsync(vm, Snapshot([Session("1", user: "bob")]));
        vm.SelectSession("1");
        _dialogs.ProductionResult = new(false, false);

        await vm.TerminateSessionCommand.ExecuteAsync(null);

        var request = Assert.Single(_dialogs.ProductionRequests);
        Assert.Equal("appdb", request.RequiredText);
        Assert.Contains("1", request.Message);
        Assert.Contains("bob", request.Message);
        Assert.Empty(_dialogs.Confirms);
        Assert.Empty(_reader.Terminated); // refused: nothing was sent to the server

        _dialogs.ProductionResult = new(true, false);
        await vm.TerminateSessionCommand.ExecuteAsync(null);
        Assert.Equal(["1"], _reader.Terminated);
    }

    [Fact]
    public async Task Cancel_OnProduction_AsksAPlainProductionConfirmation()
    {
        var vm = Create(ConnectionEnvironment.Production);
        await LoadAsync(vm, Snapshot([Session("1")]));
        vm.SelectSession("1");

        await vm.CancelQueryCommand.ExecuteAsync(null);

        Assert.Null(Assert.Single(_dialogs.ProductionRequests).RequiredText);
        Assert.Equal(["1"], _reader.Cancelled);
        Assert.Equal(UiText.Get("activity.production"), vm.ProductionHint);
    }

    [Fact]
    public async Task ActionFailure_ShowsTheMessage()
    {
        var vm = Create();
        await LoadAsync(vm, Snapshot([Session("1")]));
        vm.SelectSession("1");
        _reader.ActionError = new ActivityActionFailedException("refused", "permission denied");

        await vm.TerminateSessionCommand.ExecuteAsync(null);

        Assert.Equal("permission denied", Assert.Single(_dialogs.Errors).Message);
    }

    [Fact]
    public void NoReader_DisabledReason()
    {
        Assert.Equal(UiText.Get("activity.disabled.notConnected"), SessionsMonitorViewModel.DisabledReason(null));
    }

    // ---- fakes ------------------------------------------------------------------------------------------------------

    private sealed class FakeReader : IActivityReader
    {
        private int _reads;

        public bool SupportsCancelQuery { get; set; } = true;

        public ActivitySnapshot? Auto { get; set; }

        public int Reads => Volatile.Read(ref _reads);

        public List<TaskCompletionSource<ActivitySnapshot>> Calls { get; } = [];

        public List<string> Cancelled { get; } = [];

        public List<string> Terminated { get; } = [];

        public Exception? ActionError { get; set; }

        public ValueTask<ActivitySnapshot> ReadAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _reads);
            if (Auto is { } snapshot)
                return ValueTask.FromResult(snapshot);
            var source = new TaskCompletionSource<ActivitySnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (Calls)
                Calls.Add(source);
            return new ValueTask<ActivitySnapshot>(source.Task);
        }

        public void Complete(int call, ActivitySnapshot snapshot) => Calls[call].SetResult(snapshot);

        public void Fail(int call, Exception error) => Calls[call].SetException(error);

        public ValueTask CancelQueryAsync(string sessionId, CancellationToken cancellationToken)
        {
            if (ActionError is { } error)
                throw error;
            Cancelled.Add(sessionId);
            return ValueTask.CompletedTask;
        }

        public ValueTask TerminateSessionAsync(string sessionId, CancellationToken cancellationToken)
        {
            if (ActionError is { } error)
                throw error;
            Terminated.Add(sessionId);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ManualTime(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;

        public StubTimer Timer { get; } = new();

        public override DateTimeOffset GetUtcNow() => Now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => Timer;
    }

    private sealed class StubTimer : ITimer
    {
        public TimeSpan Period { get; private set; } = Timeout.InfiniteTimeSpan;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            Period = period;
            return true;
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
