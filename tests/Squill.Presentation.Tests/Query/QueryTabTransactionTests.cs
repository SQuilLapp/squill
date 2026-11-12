using Squill.Core;
using Squill.Core.Transactions;
using Squill.Data;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation.Editor;
using Squill.Presentation.Localization;
using Squill.Presentation.Query;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.Presentation.Tests.Query;

public sealed class QueryTabTransactionTests
{
    private sealed class Reader : IResultReader
    {
        public IReadOnlyList<ColumnInfo> Columns => [];
        public long RecordsAffected => 1;
        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken) => ValueTask.FromResult<object?[][]?>(null);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Conn : IDatabaseConnection, ITransactionProbe
    {
        public List<string> Sql { get; } = [];
        public Func<string, Exception?> Fail { get; set; } = _ => null;
        public int Probes { get; private set; }
        public TransactionProbeResult Answer { get; set; } = TransactionProbeResult.Unknown;
        public string ServerVersion => "1";

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
        {
            Sql.Add(sql);
            return Fail(sql) is { } ex ? throw ex : ValueTask.FromResult<IResultReader>(new Reader());
        }

        public ValueTask<TransactionProbeResult> ProbeAsync(CancellationToken cancellationToken)
        {
            Probes++;
            return ValueTask.FromResult(Answer);
        }

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);
        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(SchemaSnapshot.Empty);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class History : IQueryHistoryStore
    {
        public List<HistoryEntry> Entries { get; } = [];

        public ValueTask AppendAsync(HistoryEntry e, CancellationToken ct)
        {
            Entries.Add(e);
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<HistoryEntry>> ListAsync(int max, CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<HistoryEntry>>([]);
        public ValueTask ClearAsync(CancellationToken ct) => ValueTask.CompletedTask;
    }

    private sealed class Settings : ISettingsStore
    {
        public ValueTask<AppSettings> LoadAsync(CancellationToken ct) => ValueTask.FromResult(AppSettings.Default);
        public ValueTask SaveAsync(AppSettings settings, CancellationToken ct) => ValueTask.CompletedTask;
    }

    private sealed class Rig
    {
        public Rig(DatabaseDialect dialect = DatabaseDialect.PostgreSql)
        {
            Dialect = dialect;
            Session = new ConnectionSession(new ConnectionProfile(Guid.NewGuid(), "p", dialect, "h", 1, "d", "u", null, null), Connection, SchemaSnapshot.Empty, null);
            Tab = NewTab("Q1");
        }

        public DatabaseDialect Dialect { get; }
        public Conn Connection { get; } = new();
        public History History { get; } = new();
        public FakeDialogService Dialogs { get; } = new();
        public ConnectionSession Session { get; }
        public QueryTabViewModel Tab { get; }
        public StubSqlEditor Editor { get; private set; } = new();

        public QueryTabViewModel NewTab(string title)
        {
            Editor = new StubSqlEditor();
            return new QueryTabViewModel(Session, Editor, History, new Settings(), new ImmediateDispatcher(), Dialogs) { Title = title };
        }

        public async Task RunAsync(string script, QueryTabViewModel? tab = null)
        {
            Editor.Text = script;
            await (tab ?? Tab).RunAllCommand.ExecuteAsync(null);
        }
    }

    private static async Task Manual(Rig r) => await r.Session.Transaction.SetModeAsync(TransactionMode.Manual, CancellationToken.None);

    [Fact]
    public async Task Manual_select_sends_no_begin_and_writes_open_one_transaction_for_all_writes()
    {
        var r = new Rig();
        await Manual(r);

        await r.RunAsync("select 1");
        Assert.Equal(["select 1"], r.Connection.Sql);

        await r.RunAsync("insert into t values (1); insert into t values (2)");

        Assert.Equal(["select 1", "BEGIN", "insert into t values (1);", "insert into t values (2)"], r.Connection.Sql);
        Assert.Equal(TransactionState.Open, r.Session.Transaction.State);
        Assert.Equal(2, r.Session.Transaction.PendingCount);
        Assert.All(r.Session.Transaction.Journal, j => Assert.Equal("Q1", j.Tab));
    }

    [Fact]
    public async Task Auto_commit_mode_sends_no_begin_and_counts_nothing()
    {
        var r = new Rig();
        await r.RunAsync("insert into t values (1)");
        Assert.Equal(["insert into t values (1)"], r.Connection.Sql);
        Assert.Equal(TransactionState.None, r.Session.Transaction.State);
        Assert.Equal(0, r.Session.Transaction.PendingCount);
    }

    [Fact]
    public async Task A_script_stops_at_the_first_error_and_the_count_excludes_it()
    {
        var r = new Rig(DatabaseDialect.SqlServer);
        await Manual(r);
        r.Connection.Fail = sql => sql.Contains("bad", StringComparison.Ordinal) ? new InvalidOperationException("nope") : null;
        r.Connection.Answer = new(true, false);

        await r.RunAsync("insert into a values (1); insert into bad values (2); insert into c values (3)");

        Assert.Equal(["BEGIN TRANSACTION", "insert into a values (1);", "insert into bad values (2);"], r.Connection.Sql);
        Assert.Equal(1, r.Session.Transaction.PendingCount);
        Assert.Equal(TransactionState.Open, r.Session.Transaction.State);
        Assert.Equal(1, r.Connection.Probes);
    }

    [Fact]
    public async Task A_postgres_error_fails_the_transaction_then_refuses_statements_until_a_typed_rollback()
    {
        var r = new Rig();
        await Manual(r);
        r.Connection.Fail = sql => sql.Contains("bad", StringComparison.Ordinal) ? new InvalidOperationException("nope") : null;

        await r.RunAsync("insert into bad values (1)");
        Assert.Equal(TransactionState.Failed, r.Session.Transaction.State);

        r.Connection.Sql.Clear();
        await r.RunAsync("select 1");
        Assert.Empty(r.Connection.Sql);
        Assert.NotEmpty(r.Tab.Results);
        Assert.False(r.History.Entries[^1].Succeeded);

        await r.RunAsync("ROLLBACK");
        Assert.Equal(["ROLLBACK"], r.Connection.Sql);
        Assert.Equal(TransactionState.None, r.Session.Transaction.State);
    }

    [Fact]
    public async Task A_statement_that_cannot_run_in_a_transaction_is_refused_and_not_sent()
    {
        var r = new Rig();
        await Manual(r);
        await r.RunAsync("insert into t values (1)");
        r.Connection.Sql.Clear();

        await r.RunAsync("VACUUM");

        Assert.Empty(r.Connection.Sql);
        Assert.Equal(TransactionState.Open, r.Session.Transaction.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mysql_ddl_inside_an_open_transaction_asks_first(bool accept)
    {
        var r = new Rig(DatabaseDialect.MySql);
        await Manual(r);
        await r.RunAsync("insert into t values (1)");
        r.Connection.Sql.Clear();
        r.Dialogs.ConfirmResult = accept;

        await r.RunAsync("create table x (a int)");

        Assert.Contains(r.Dialogs.Confirms, c => c.Title == UiText.Get("tx.implicitCommit.title"));
        Assert.Equal(accept ? ["create table x (a int)"] : [], r.Connection.Sql);
        Assert.Equal(accept ? TransactionState.None : TransactionState.Open, r.Session.Transaction.State);
    }

    [Fact]
    public async Task A_typed_begin_in_auto_commit_opens_a_transaction_and_enables_valider()
    {
        var r = new Rig();
        await r.RunAsync("BEGIN");
        Assert.Equal(TransactionState.Open, r.Session.Transaction.State);
        Assert.Equal(TransactionMode.AutoCommit, r.Session.Transaction.Mode);
        Assert.True(r.Tab.CommitCommand.CanExecute(null));
        Assert.True(r.Tab.RollbackCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(DatabaseDialect.MySql, "COMMIT", "ROLLBACK")]
    [InlineData(DatabaseDialect.SqlServer, "COMMIT TRANSACTION", "ROLLBACK TRANSACTION")]
    [InlineData(DatabaseDialect.PostgreSql, "COMMIT", "ROLLBACK")]
    public async Task Valider_and_Annuler_send_the_dialect_text_write_history_and_reset_the_count(DatabaseDialect d, string commit, string rollback)
    {
        var r = new Rig(d);
        await Manual(r);
        foreach (var (command, text, shown) in new[] { (r.Tab.CommitCommand, commit, "COMMIT"), (r.Tab.RollbackCommand, rollback, "ROLLBACK") })
        {
            await r.RunAsync("insert into t values (1)");
            Assert.Equal(1, r.Session.Transaction.PendingCount);

            await command.ExecuteAsync(null);

            Assert.Equal(text, r.Connection.Sql[^1]);
            Assert.Equal(0, r.Session.Transaction.PendingCount);
            Assert.Equal(TransactionState.None, r.Session.Transaction.State);
            Assert.Equal((shown, true), (r.History.Entries[^1].Sql, r.History.Entries[^1].Succeeded));
            Assert.Equal(r.Session.Profile.Id, r.History.Entries[^1].ProfileId);
        }
    }

    [Fact]
    public async Task A_commit_error_keeps_the_state_and_shows_the_message()
    {
        var r = new Rig();
        await Manual(r);
        await r.RunAsync("insert into t values (1)");
        r.Connection.Fail = sql => sql == "COMMIT" ? new InvalidOperationException("commit boom") : null;

        await r.Tab.CommitCommand.ExecuteAsync(null);

        Assert.Equal(TransactionState.Open, r.Session.Transaction.State);
        Assert.Equal("commit boom", r.Tab.StatusText);
        Assert.False(r.History.Entries[^1].Succeeded);
    }

    [Fact]
    public async Task Commands_are_disabled_while_running_and_a_second_tab_sees_the_same_transaction()
    {
        var r = new Rig();
        await Manual(r);
        await r.RunAsync("insert into t values (1)");
        var other = r.NewTab("Q2");

        Assert.Same(r.Tab.Transaction, other.Transaction);
        Assert.True(other.CommitCommand.CanExecute(null));

        using (r.Session.Transaction.BeginOperation())
        {
            Assert.False(r.Tab.CommitCommand.CanExecute(null));
            Assert.False(other.RollbackCommand.CanExecute(null));
            await r.RunAsync("insert into t values (2)", other);
            Assert.Equal(UiText.Get("tx.busy"), other.StatusText);
        }

        Assert.True(other.CommitCommand.CanExecute(null));
        await r.RunAsync("insert into t values (2)", other);
        Assert.Equal(2, r.Session.Transaction.PendingCount);
        Assert.Equal("Q2", r.Session.Transaction.Journal[^1].Tab);
    }

    [Fact]
    public async Task A_statement_that_may_change_the_transaction_probes_the_server()
    {
        var r = new Rig(DatabaseDialect.MySql);
        r.Connection.Answer = new(true, false);

        await r.RunAsync("SET autocommit = 0");

        Assert.Equal(1, r.Connection.Probes);
        Assert.Equal(TransactionState.Open, r.Session.Transaction.State);
    }

    [Fact]
    public async Task Switching_to_auto_commit_with_an_open_transaction_asks_and_Back_keeps_manual_mode()
    {
        var r = new Rig();
        await Manual(r);
        await r.RunAsync("insert into t values (1)");

        await r.Tab.SetModeCommand.ExecuteAsync(TransactionMode.AutoCommit);

        Assert.Equal(TransactionMode.Manual, r.Session.Transaction.Mode);
        var request = Assert.Single(r.Dialogs.TransactionRequests);
        Assert.Equal(UiText.Get("tx.ask.switchMode"), request.Title);
        await r.Tab.RollbackCommand.ExecuteAsync(null);
        await r.Tab.SetModeCommand.ExecuteAsync(TransactionMode.AutoCommit);
        Assert.Equal(TransactionMode.AutoCommit, r.Session.Transaction.Mode);
    }

    [Fact]
    public void Shortcuts_are_the_documented_ones()
    {
        Assert.Equal("Ctrl+Alt+C", QueryTabViewModel.CommitShortcut);
        Assert.Equal("Ctrl+Alt+Z", QueryTabViewModel.RollbackShortcut);
    }
}
