using Squill.Core;
using Squill.Data;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation.Editor;
using Squill.Presentation.Query;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.Presentation.Tests.Query;

public sealed class QueryTabViewModelTests
{
    private static readonly ColumnInfo Col = new("c", "int", typeof(int), true, 0);

    private sealed class FakeReader(bool grid, int rows, long affected) : IResultReader
    {
        private bool _done;
        public bool Disposed { get; private set; }
        public IReadOnlyList<ColumnInfo> Columns { get; } = grid ? [Col] : [];
        public long RecordsAffected => affected;

        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_done)
                return ValueTask.FromResult<object?[][]?>(null);
            _done = true;
            return ValueTask.FromResult<object?[][]?>([.. Enumerable.Range(0, rows).Select(i => new object?[] { i })]);
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeConnection : IDatabaseConnection
    {
        public List<string> Executed { get; } = [];
        public List<FakeReader> Readers { get; } = [];
        public Func<string, CancellationToken, Task>? Gate { get; set; }
        public int SchemaReads { get; private set; }
        public string ServerVersion => "1";

        public async ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
        {
            Executed.Add(sql);
            if (Gate is not null)
                await Gate(sql, cancellationToken);
            if (sql.Contains("boom", StringComparison.Ordinal))
                throw new InvalidOperationException("boom failed");
            var reader = sql.StartsWith("SELECT", StringComparison.Ordinal)
                ? new FakeReader(true, 3, -1)
                : new FakeReader(false, 0, sql.StartsWith("UPDATE", StringComparison.Ordinal) ? 5 : -1);
            Readers.Add(reader);
            return reader;
        }

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken)
        {
            SchemaReads++;
            return ValueTask.FromResult(SchemaSnapshot.Empty);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeHistory : IQueryHistoryStore
    {
        public List<HistoryEntry> Entries { get; } = [];

        public ValueTask AppendAsync(HistoryEntry e, CancellationToken ct)
        {
            Entries.Add(e);
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<HistoryEntry>> ListAsync(int max, CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<HistoryEntry>>(Entries);

        public ValueTask ClearAsync(CancellationToken ct) => ValueTask.CompletedTask;
    }

    private sealed class FakeSettings : ISettingsStore
    {
        public ValueTask<AppSettings> LoadAsync(CancellationToken ct) => ValueTask.FromResult(AppSettings.Default);

        public ValueTask SaveAsync(AppSettings settings, CancellationToken ct) => ValueTask.CompletedTask;
    }

    private sealed class Fixture
    {
        public Fixture(string script)
        {
            Connection = new FakeConnection();
            Editor = new StubSqlEditor { Text = script };
            var session = new ConnectionSession(
                new ConnectionProfile(Guid.NewGuid(), "p", DatabaseDialect.PostgreSql, "h", 1, "d", "u", null, null),
                Connection, SchemaSnapshot.Empty, null);
            Vm = new QueryTabViewModel(session, Editor, History, new FakeSettings(), new ImmediateDispatcher(), new FakeDialogService());
        }

        public FakeConnection Connection { get; }
        public StubSqlEditor Editor { get; }
        public FakeHistory History { get; } = new();
        public QueryTabViewModel Vm { get; }
    }

    [Fact]
    public async Task Run_SingleStatement_ProducesGridAndHistory()
    {
        var f = new Fixture("SELECT 1;");

        await f.Vm.RunCommand.ExecuteAsync(null);

        var result = Assert.Single(f.Vm.Results);
        Assert.Equal(ResultKind.Grid, result.Kind);
        Assert.Equal("Result 1", result.Title);
        Assert.Equal(3, result.Grid!.RowCount);
        Assert.Same(result, f.Vm.SelectedResult);
        Assert.False(f.Vm.IsRunning);
        Assert.True(f.Connection.Readers[0].Disposed);
        var entry = Assert.Single(f.History.Entries);
        Assert.True(entry.Succeeded);
        Assert.Equal(3, entry.RowCount);
        Assert.Equal("SELECT 1;", entry.Sql);
    }

    [Fact]
    public async Task RunAll_ThreeStatements_RunsInOrder()
    {
        var f = new Fixture("SELECT 1; SELECT 2; SELECT 3;");

        await f.Vm.RunAllCommand.ExecuteAsync(null);

        Assert.Equal(["Result 1", "Result 2", "Result 3"], f.Vm.Results.Select(r => r.Title));
        Assert.Equal(3, f.History.Entries.Count);
        Assert.Equal(3, f.Connection.Executed.Count);
    }

    [Fact]
    public async Task RunAll_ErrorInSecond_StopsAndKeepsPreviousResults()
    {
        var f = new Fixture("SELECT 1; SELECT boom; SELECT 3;");

        await f.Vm.RunAllCommand.ExecuteAsync(null);

        Assert.Equal(2, f.Vm.Results.Count);
        Assert.Equal(ResultKind.Grid, f.Vm.Results[0].Kind);
        Assert.Equal(ResultKind.Message, f.Vm.Results[1].Kind);
        Assert.Equal("boom failed", f.Vm.Results[1].Text);
        Assert.Equal(2, f.Connection.Executed.Count);
        Assert.Equal([true, false], f.History.Entries.Select(e => e.Succeeded));
    }

    [Fact]
    public async Task Run_Dml_ShowsRowsAffectedMessage()
    {
        var f = new Fixture("UPDATE t SET a = 1;");

        await f.Vm.RunCommand.ExecuteAsync(null);

        Assert.Equal("5 row(s) affected", Assert.Single(f.Vm.Results).Text);
        Assert.Equal(5, f.History.Entries[0].RowCount);
    }

    [Fact]
    public async Task Run_StatementWithoutCount_ShowsCommandCompleted()
    {
        var f = new Fixture("TRUNCATE t;");

        await f.Vm.RunCommand.ExecuteAsync(null);

        Assert.Equal("Command completed", Assert.Single(f.Vm.Results).Text);
    }

    [Fact]
    public async Task Run_Ddl_RefreshesSchema()
    {
        var f = new Fixture("CREATE TABLE t (a int);");

        await f.Vm.RunCommand.ExecuteAsync(null);

        Assert.Equal(1, f.Connection.SchemaReads);
    }

    [Fact]
    public async Task Cancel_WhileRunning_StopsTheRun()
    {
        var f = new Fixture("SELECT 1; SELECT 2;");
        var started = new TaskCompletionSource();
        f.Connection.Gate = async (_, ct) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
        };

        var run = f.Vm.RunAllCommand.ExecuteAsync(null);
        await started.Task;
        Assert.True(f.Vm.IsRunning);
        f.Vm.CancelCommand.Execute(null);
        await run;

        Assert.False(f.Vm.IsRunning);
        Assert.Equal("Cancelled", f.Vm.StatusText);
        Assert.Single(f.Connection.Executed);
        Assert.False(Assert.Single(f.History.Entries).Succeeded);
    }

    [Fact]
    public async Task Run_WhileRunning_IsIgnored()
    {
        var f = new Fixture("SELECT 1;");
        var release = new TaskCompletionSource();
        var started = new TaskCompletionSource();
        f.Connection.Gate = async (_, _) =>
        {
            started.TrySetResult();
            await release.Task;
        };

        var first = f.Vm.RunCommand.ExecuteAsync(null);
        await started.Task;
        await f.Vm.RunCommand.ExecuteAsync(null);
        release.SetResult();
        await first;

        Assert.Single(f.Connection.Executed);
        Assert.Single(f.Vm.Results);
    }

    [Fact]
    public async Task Run_Again_ReplacesPreviousResults()
    {
        var f = new Fixture("SELECT 1;");

        await f.Vm.RunCommand.ExecuteAsync(null);
        await f.Vm.RunCommand.ExecuteAsync(null);

        Assert.Single(f.Vm.Results);
        Assert.Equal(2, f.History.Entries.Count);
    }

    [Fact]
    public void Constructor_WiresEditor()
    {
        var f = new Fixture("SELECT 1;");

        Assert.Equal(DatabaseDialect.PostgreSql, f.Editor.Dialect);
        Assert.NotNull(f.Editor.CompletionProvider);
        Assert.NotNull(f.Editor.HoverProvider);
    }
}
