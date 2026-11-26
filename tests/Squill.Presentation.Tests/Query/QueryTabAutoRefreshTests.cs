using Squill.Core;
using Squill.Core.Transactions;
using Squill.Data;
using Squill.Data.Sqlite;
using Squill.Presentation.Editor;
using Squill.Presentation.Localization;
using Squill.Presentation.Query;
using Squill.Presentation.Sessions;
using Squill.Sql.Transactions;
using Xunit;

namespace Squill.Presentation.Tests.Query;

public sealed class QueryTabAutoRefreshTests : IAsyncLifetime
{
    private static readonly TimeSpan Five = TimeSpan.FromSeconds(5);

    private readonly string _file = Path.Combine(Path.GetTempPath(), $"squill-{Guid.NewGuid():N}.db");
    private readonly FakeDialogService _dialogs = new();
    private readonly QueryTabRefreshTests.GatedConnection _gated = new();
    private readonly Clock _clock = new();
    private ConnectionProfile _profile = null!;
    private SchemaSnapshot _schema = null!;
    private StubSqlEditor _editor = null!;
    private QueryTabViewModel _tab = null!;

    public async ValueTask InitializeAsync()
    {
        _profile = new ConnectionProfile(Guid.NewGuid(), "p", DatabaseDialect.Sqlite, null, null, null, null, _file, null);
        _gated.Inner = await new SqliteDatabaseProvider().OpenAsync(_profile, null, CancellationToken.None);
        await Exec("CREATE TABLE t (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
        await Exec("INSERT INTO t (name) VALUES ('a'), ('b')");
        _schema = await _gated.Inner.ReadSchemaAsync(CancellationToken.None);
        (_tab, _editor) = NewTab(_profile);
    }

    public async ValueTask DisposeAsync()
    {
        _tab.StopAutoRefresh();
        await _gated.Inner.DisposeAsync();
        try
        {
            File.Delete(_file);
        }
        catch (IOException)
        {
        }
    }

    private (QueryTabViewModel Tab, StubSqlEditor Editor) NewTab(ConnectionProfile profile)
    {
        var editor = new StubSqlEditor();
        var tab = new QueryTabViewModel(new ConnectionSession(profile, _gated, _schema, null), editor, new QueryTabRefreshTests.History(), new QueryTabRefreshTests.Settings(), new ImmediateDispatcher(), _dialogs, _clock);
        return (tab, editor);
    }

    private async Task Exec(string sql) => await (await _gated.Inner.ExecuteAsync(sql, [], CancellationToken.None)).DisposeAsync();

    private async Task RunAsync(string sql = "SELECT * FROM t")
    {
        _editor.Text = sql;
        await _tab.RunCommand.ExecuteAsync(null);
    }

    private async Task ReadyAsync(TimeSpan? interval = null)
    {
        await RunAsync();
        _tab.SetAutoRefreshCommand.Execute(interval ?? Five);
        _gated.Executed.Clear();
    }

    private async Task AdvanceAsync(TimeSpan by)
    {
        _clock.Timer!.Advance(by);
        await _tab.Idle;
    }

    [Fact]
    public async Task Default_Disabled()
    {
        await RunAsync();
        Assert.Null(_tab.AutoRefreshInterval);
        Assert.Equal("", _tab.AutoRefreshLabel);
        Assert.False(_tab.IsAutoRefreshCustom);
    }

    [Fact]
    public async Task SetAutoRefresh_ArmsTimer_NoImmediateRefresh()
    {
        await ReadyAsync();
        Assert.Equal(Five, _clock.Timer!.Due);
        Assert.Empty(_gated.Executed);
        Assert.Equal("5 s", _tab.AutoRefreshLabel);
    }

    [Fact]
    public async Task Tick_AfterInterval_Refreshes_AndRearms()
    {
        await ReadyAsync();

        await AdvanceAsync(TimeSpan.FromSeconds(4));
        Assert.Empty(_gated.Executed);
        await AdvanceAsync(TimeSpan.FromSeconds(1));
        Assert.Single(_gated.Executed);
        Assert.Equal(Five, _clock.Timer!.Due);
        await AdvanceAsync(Five);
        Assert.Equal(2, _gated.Executed.Count);
    }

    [Fact]
    public async Task SlowRefresh_NeverOverlaps()
    {
        await ReadyAsync();
        var gate = new TaskCompletionSource();
        _gated.Gate = (_, _) => gate.Task;

        _clock.Timer!.Advance(Five);
        Assert.True(_tab.IsRefreshing);
        _clock.Timer.Advance(TimeSpan.FromSeconds(15));
        Assert.Single(_gated.Executed);
        Assert.Equal(Timeout.InfiniteTimeSpan, _clock.Timer.Due);

        gate.SetResult();
        await _tab.Idle;
        Assert.Equal(Five, _clock.Timer.Due);
        Assert.Single(_gated.Executed);
    }

    [Fact]
    public async Task PendingChanges_Paused_NoSql_ReasonSet_ThenResumes()
    {
        await ReadyAsync();
        Assert.Null(_tab.Results[0].Editor!.TrySetCell(0, 1, "changed"));

        await AdvanceAsync(Five);

        Assert.Empty(_gated.Executed);
        Assert.Equal(UiText.Get("query.autoRefresh.paused.pending"), _tab.AutoRefreshPausedReason);
        Assert.Contains(UiText.Get("query.autoRefresh.paused.pending"), _tab.AutoRefreshToolTip);
        Assert.Equal(Five, _clock.Timer!.Due);

        await _tab.Results[0].Editor!.DiscardCommand.ExecuteAsync(null);
        await AdvanceAsync(Five);

        Assert.Single(_gated.Executed);
        Assert.Equal("", _tab.AutoRefreshPausedReason);
    }

    [Fact]
    public async Task Editing_Paused()
    {
        await ReadyAsync();
        _tab.Results[0].Selection!.SelectCell(0, 1, false);
        Assert.True(_tab.Results[0].EditController!.Begin());

        await AdvanceAsync(Five);

        Assert.Empty(_gated.Executed);
        Assert.Equal(UiText.Get("query.autoRefresh.paused.editing"), _tab.AutoRefreshPausedReason);
    }

    [Fact]
    public async Task FailedTransaction_Paused()
    {
        var (tab, editor) = NewTab(_profile with { Dialect = DatabaseDialect.PostgreSql });
        editor.Text = "SELECT * FROM t";
        await tab.RunCommand.ExecuteAsync(null);
        tab.SetAutoRefreshCommand.Execute(Five);
        var tx = tab.Transaction;
        tx.ObserveSuccess("BEGIN", StatementClassifier.Classify("BEGIN", DatabaseDialect.PostgreSql), -1, "Q");
        await tx.ObserveFailureAsync(new InvalidOperationException(), StatementClassifier.Classify("UPDATE t SET id = 1", DatabaseDialect.PostgreSql), CancellationToken.None);
        Assert.Equal(TransactionState.Failed, tx.State);
        _gated.Executed.Clear();

        _clock.Timer!.Advance(Five);
        await tab.Idle;

        Assert.Empty(_gated.Executed);
        Assert.Equal(UiText.Get("query.autoRefresh.paused.transaction"), tab.AutoRefreshPausedReason);
        tab.StopAutoRefresh();
    }

    [Fact]
    public async Task OpenManualTransaction_StillRefreshes()
    {
        var (tab, editor) = NewTab(_profile with { Dialect = DatabaseDialect.PostgreSql });
        editor.Text = "SELECT * FROM t";
        await tab.RunCommand.ExecuteAsync(null);
        tab.SetAutoRefreshCommand.Execute(Five);
        tab.Transaction.ObserveSuccess("BEGIN", StatementClassifier.Classify("BEGIN", DatabaseDialect.PostgreSql), -1, "Q");
        Assert.Equal(TransactionState.Open, tab.Transaction.State);
        _gated.Executed.Clear();

        _clock.Timer!.Advance(Five);
        await tab.Idle;

        Assert.Equal("", tab.AutoRefreshPausedReason);
        Assert.Contains("SELECT * FROM t", _gated.Executed);
        tab.StopAutoRefresh();
    }

    [Fact]
    public async Task Failed_StopsAndSetsStatus()
    {
        await ReadyAsync();
        await Exec("DROP TABLE t");

        await AdvanceAsync(Five);

        Assert.Null(_tab.AutoRefreshInterval);
        Assert.Equal(UiText.Get("query.autoRefresh.stopped"), _tab.StatusText);
        Assert.Equal(Timeout.InfiniteTimeSpan, _clock.Timer!.Due);
    }

    [Fact]
    public async Task CancelledDuringAutoRefresh_Disables()
    {
        await ReadyAsync();
        var started = new TaskCompletionSource();
        _gated.Gate = async (_, ct) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        };

        _clock.Timer!.Advance(Five);
        await started.Task;
        _tab.CancelCommand.Execute(null);
        await _tab.Idle;

        Assert.Null(_tab.AutoRefreshInterval);
    }

    [Fact]
    public async Task Hidden_NoRefresh_ShownAgain_RearmsFullInterval()
    {
        await ReadyAsync();

        _tab.SetActive(false);
        Assert.Equal(Timeout.InfiniteTimeSpan, _clock.Timer!.Due);
        await AdvanceAsync(TimeSpan.FromMinutes(1));
        Assert.Empty(_gated.Executed);

        _tab.SetActive(true);
        Assert.Equal(Five, _clock.Timer.Due);
        Assert.Empty(_gated.Executed);
    }

    [Fact]
    public async Task StopAutoRefresh_DisposesTimer_AndIgnoresLaterTicks()
    {
        await ReadyAsync();

        _tab.StopAutoRefresh();
        _tab.Tick();
        _tab.SetAutoRefreshCommand.Execute(Five);

        Assert.True(_clock.Timer!.Disposed);
        Assert.Null(_tab.AutoRefreshInterval);
        Assert.Empty(_gated.Executed);
    }

    [Fact]
    public async Task NewDmlRun_Disables()
    {
        await ReadyAsync();
        await RunAsync("INSERT INTO t (name) VALUES ('z')");
        Assert.Null(_tab.AutoRefreshInterval);
        Assert.Equal(Timeout.InfiniteTimeSpan, _clock.Timer!.Due);
    }

    [Fact]
    public async Task NewSelectRun_KeepsInterval()
    {
        await ReadyAsync();
        await RunAsync("SELECT id FROM t");
        Assert.Equal(Five, _tab.AutoRefreshInterval);
        Assert.Equal(Five, _clock.Timer!.Due);
    }

    [Fact]
    public void SetAutoRefresh_WhileCannotRefresh_Ignored()
    {
        _tab.SetAutoRefreshCommand.Execute(Five);
        Assert.Null(_tab.AutoRefreshInterval);
        Assert.Contains(UiText.Get("query.autoRefresh.unavailable"), _tab.AutoRefreshToolTip);
    }

    [Fact]
    public async Task SetAutoRefresh_OutOfRange_Ignored()
    {
        await RunAsync();
        _tab.SetAutoRefreshCommand.Execute(TimeSpan.FromSeconds(3601));
        _tab.SetAutoRefreshCommand.Execute(TimeSpan.Zero);
        Assert.Null(_tab.AutoRefreshInterval);
    }

    [Fact]
    public async Task Custom_Valid_SetsInterval()
    {
        await RunAsync();
        _dialogs.PromptResult = " 45 ";
        await _tab.SetCustomAutoRefreshCommand.ExecuteAsync(null);
        Assert.Equal(TimeSpan.FromSeconds(45), _tab.AutoRefreshInterval);
        Assert.True(_tab.IsAutoRefreshCustom);
        Assert.Equal("45 s", _tab.AutoRefreshLabel);
    }

    [Fact]
    public async Task Custom_EqualToPreset_SelectsPreset()
    {
        await RunAsync();
        _dialogs.PromptResult = "300";
        await _tab.SetCustomAutoRefreshCommand.ExecuteAsync(null);
        Assert.Equal(TimeSpan.FromMinutes(5), _tab.AutoRefreshInterval);
        Assert.False(_tab.IsAutoRefreshCustom);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("3601")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("-5")]
    public async Task Custom_Invalid_ShowsErrorKeepsInterval(string text)
    {
        await ReadyAsync();
        _dialogs.PromptResult = text;
        await _tab.SetCustomAutoRefreshCommand.ExecuteAsync(null);
        Assert.Equal(Five, _tab.AutoRefreshInterval);
        Assert.Equal((UiText.Get("query.autoRefresh.customTitle"), UiText.Get("query.autoRefresh.customInvalid")), Assert.Single(_dialogs.Errors));
    }

    [Fact]
    public async Task Custom_Cancelled_NoChange()
    {
        await ReadyAsync();
        _dialogs.PromptCancelled = true;
        await _tab.SetCustomAutoRefreshCommand.ExecuteAsync(null);
        Assert.Equal(Five, _tab.AutoRefreshInterval);
        Assert.Empty(_dialogs.Errors);
    }

    [Fact]
    public void FormatInterval_SecondsAndMinutes()
    {
        int[] seconds = [5, 90, 60, 300];
        using (UiText.Override(UiLanguage.English))
            Assert.Equal(["5 s", "90 s", "1 m", "5 m"], seconds.Select(s => QueryTabViewModel.FormatInterval(TimeSpan.FromSeconds(s))));
        using (UiText.Override(UiLanguage.French))
            Assert.Equal(["5 s", "90 s", "1 min", "5 min"], seconds.Select(s => QueryTabViewModel.FormatInterval(TimeSpan.FromSeconds(s))));
    }

    [Fact]
    public async Task ToolTip_ShowsIntervalAndPausedReason()
    {
        await RunAsync();
        Assert.Equal(UiText.Get("query.autoRefresh.off"), _tab.AutoRefreshToolTip);
        _tab.SetAutoRefreshCommand.Execute(TimeSpan.FromSeconds(30));
        Assert.Equal(UiText.Format("query.autoRefresh.every", "30 s"), _tab.AutoRefreshToolTip);
        Assert.Null(_tab.Results[0].Editor!.TrySetCell(0, 1, "x"));
        await AdvanceAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(UiText.Format("query.autoRefresh.every", "30 s") + "\n" + UiText.Get("query.autoRefresh.paused.pending"), _tab.AutoRefreshToolTip);
    }

    /// <summary>Hands the tab a manual timer; every other timer (the diagnostics debounce) is a real one.</summary>
    private sealed class Clock : TimeProvider
    {
        public ManualTimer? Timer { get; private set; }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            if (state is not QueryTabViewModel)
                return base.CreateTimer(callback, state, dueTime, period);
            return Timer = new ManualTimer(callback, state, dueTime);
        }
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, TimeSpan due) : ITimer
    {
        public TimeSpan Due { get; private set; } = due;

        public bool Disposed { get; private set; }

        public void Advance(TimeSpan by)
        {
            if (Due == Timeout.InfiniteTimeSpan || Disposed)
                return;
            Due -= by;
            if (Due > TimeSpan.Zero)
                return;
            Due = Timeout.InfiniteTimeSpan; // one-shot
            callback(state);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            Due = dueTime;
            return true;
        }

        public void Dispose() => Disposed = true;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
