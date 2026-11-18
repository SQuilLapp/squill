using Squill.Core;
using Squill.Data;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation.Editor;
using Squill.Presentation.Query;
using Squill.Presentation.Sessions;
using Squill.Tests.Shared;
using Xunit;

namespace Squill.Presentation.Tests.Query;

public sealed class QueryTabDiagnosticsTests
{
    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly List<ManualTimer> _timers = [];

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(callback, state, dueTime);
            lock (_timers)
                _timers.Add(timer);
            return timer;
        }

        public int TimerCount
        {
            get
            {
                lock (_timers)
                    return _timers.Count;
            }
        }

        public void Advance(TimeSpan by)
        {
            ManualTimer[] due;
            lock (_timers)
                due = [.. _timers];
            foreach (var t in due)
                t.Advance(by);
        }
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, TimeSpan due) : ITimer
    {
        private TimeSpan _remaining = due;
        private bool _fired;
        private bool _disposed;

        public void Advance(TimeSpan by)
        {
            if (_fired || _disposed)
                return;
            _remaining -= by;
            if (_remaining <= TimeSpan.Zero)
            {
                _fired = true;
                callback(state);
            }
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            _remaining = dueTime;
            return true;
        }

        public void Dispose() => _disposed = true;

        public ValueTask DisposeAsync()
        {
            _disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Queues the posts so a test decides when a computed result reaches the editor.</summary>
    private sealed class QueueDispatcher : IUiDispatcher
    {
        private readonly Queue<Action> _queue = new();

        public int Pending
        {
            get
            {
                lock (_queue)
                    return _queue.Count;
            }
        }

        public bool CheckAccess() => true;

        public void Post(Action action)
        {
            lock (_queue)
                _queue.Enqueue(action);
        }

        public void Flush()
        {
            while (true)
            {
                Action a;
                lock (_queue)
                {
                    if (_queue.Count == 0)
                        return;
                    a = _queue.Dequeue();
                }

                a();
            }
        }
    }

    private sealed class SchemaConnection(SchemaSnapshot next) : IDatabaseConnection
    {
        public string ServerVersion => "1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(next);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Store : ISettingsStore
    {
        public ValueTask<AppSettings> LoadAsync(CancellationToken ct) => ValueTask.FromResult(AppSettings.Default);

        public ValueTask SaveAsync(AppSettings settings, CancellationToken ct) => ValueTask.CompletedTask;
    }

    private sealed class History : IQueryHistoryStore
    {
        public ValueTask AppendAsync(HistoryEntry e, CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<HistoryEntry>> ListAsync(int max, CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<HistoryEntry>>([]);

        public ValueTask ClearAsync(CancellationToken ct) => ValueTask.CompletedTask;
    }

    private sealed class Fixture
    {
        public Fixture(string text = "select 1", IDatabaseConnection? connection = null)
        {
            var profile = new ConnectionProfile(Guid.NewGuid(), "p", DatabaseDialect.PostgreSql, "h", 1, "d", "u", null, null);
            Session = new ConnectionSession(profile, connection!, SchemaSnapshot.Empty, null);
            Editor = new StubSqlEditor { Text = text };
            Vm = new QueryTabViewModel(Session, Editor, new History(), new Store(), Dispatcher, new FakeDialogService(), Time);
        }

        public ManualTimeProvider Time { get; } = new();
        public QueueDispatcher Dispatcher { get; } = new();
        public ConnectionSession Session { get; }
        public StubSqlEditor Editor { get; }
        public QueryTabViewModel Vm { get; }
    }

    private static void WaitFor(Func<bool> condition)
    {
        Assert.True(SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(10)), "timed out");
    }

    // The delay timer is created on a pool thread, so keep advancing the clock until the awaited effect shows up.
    private static void AdvanceUntil(Fixture f, Func<bool> condition) =>
        WaitFor(() =>
        {
            f.Time.Advance(Delay);
            return condition();
        });

    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(250);

    [Fact]
    public void FirstCallIsImmediate()
    {
        var f = new Fixture("select * from nope");
        Assert.NotEmpty(f.Editor.Diagnostics);
        Assert.Equal(0, f.Dispatcher.Pending);
    }

    [Fact]
    public void ManyQuickChangesProduceOneApplicationAfterTheDelay()
    {
        var f = new Fixture();
        for (var i = 0; i < 10; i++)
            f.Editor.Text = $"select * from nope{i}";

        AdvanceUntil(f, () => f.Dispatcher.Pending >= 1);
        Thread.Sleep(50);
        Assert.Equal(1, f.Dispatcher.Pending);
        f.Dispatcher.Flush();
        Assert.Contains(f.Editor.Diagnostics, d => d.Message.Contains("nope9", StringComparison.Ordinal));
    }

    [Fact]
    public void NothingIsAppliedBeforeTheDelay()
    {
        var f = new Fixture();
        f.Editor.Text = "select * from nope";
        f.Time.Advance(Delay / 2);
        Thread.Sleep(50);
        Assert.Equal(0, f.Dispatcher.Pending);
    }

    [Fact]
    public void AnOlderResultNeverOverwritesANewerText()
    {
        var f = new Fixture();
        f.Editor.Text = "select * from nope";
        AdvanceUntil(f, () => f.Dispatcher.Pending == 1);

        f.Editor.Text = "select 1"; // newer text arrives before the first result is applied
        f.Dispatcher.Flush();
        Assert.Empty(f.Editor.Diagnostics);
    }

    [Fact]
    public void ClosingTheTabCancelsAPendingRun()
    {
        var f = new Fixture();
        f.Editor.Text = "select * from nope";
        // The timer is created on a pool thread: cancel only once it exists, or a late timer is never disposed by the cancel.
        WaitFor(() => f.Time.TimerCount > 0);
        f.Vm.CancelPendingDiagnostics();
        f.Time.Advance(Delay);
        Thread.Sleep(100);
        Assert.Equal(0, f.Dispatcher.Pending);
    }

    [Fact]
    public async Task SchemaRefreshAppliesWithoutWaitingForTheDelay()
    {
        var table = new TableInfo("public", "nope", TableKind.Table, [new ColumnDefinition("id", "int", false, true, null)], [], []);
        var f = new Fixture("select * from nope", new SchemaConnection(new SchemaSnapshot("public", [table])));
        Assert.NotEmpty(f.Editor.Diagnostics);

        await f.Vm.RefreshSchemaAsync(TestContext.Current.CancellationToken);

        Assert.Empty(f.Editor.Diagnostics);
    }
}
