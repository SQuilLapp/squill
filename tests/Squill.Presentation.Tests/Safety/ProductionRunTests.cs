using Squill.Core;
using Squill.Core.Plans;
using Squill.Core.Transactions;
using Squill.Data;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation.Editor;
using Squill.Presentation.Localization;
using Squill.Presentation.Query;
using Squill.Presentation.Safety;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.Presentation.Tests.Safety;

public sealed class ProductionRunTests
{
    private sealed class Reader : IResultReader
    {
        public IReadOnlyList<ColumnInfo> Columns => [];
        public long RecordsAffected => 1;
        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken) => ValueTask.FromResult<object?[][]?>(null);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Conn : IDatabaseConnection, ITransactionProbe, IPlanReader
    {
        public List<string> Sql { get; } = [];
        public bool PlanFails { get; set; }
        public string ServerVersion => "1";
        public bool SupportsActualPlan => false;

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
        {
            Sql.Add(sql);
            return ValueTask.FromResult<IResultReader>(new Reader());
        }

        public ValueTask<QueryPlan> ExplainAsync(string statement, IReadOnlyList<QueryParameter> parameters, PlanMode mode, CancellationToken cancellationToken)
        {
            if (PlanFails)
                throw new InvalidOperationException("no plan");
            var scan = new PlanNode(1, "Seq Scan", PlanNodeKind.Other, null, null, 1000, null, null, null, null, null, null, [], [], false, []);
            var root = new PlanNode(0, "ModifyTable", PlanNodeKind.Other, null, null, 0, null, null, null, null, null, null, [], [], false, [scan]);
            return ValueTask.FromResult(new QueryPlan(DatabaseDialect.PostgreSql, mode, statement, root, [], [], null, null, null, false, PlanSourceFormat.Json, "{}"));
        }

        public ValueTask<TransactionProbeResult> ProbeAsync(CancellationToken cancellationToken) => ValueTask.FromResult(TransactionProbeResult.Unknown);
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
        public Rig(ConnectionEnvironment env = ConnectionEnvironment.Production)
        {
            Session = new ConnectionSession(new ConnectionProfile(Guid.NewGuid(), "prod-pg", DatabaseDialect.PostgreSql, "h", 1, "shop", "u", null, null, env), Connection, SchemaSnapshot.Empty, null);
            Tab = new QueryTabViewModel(Session, Editor, History, new Settings(), new ImmediateDispatcher(), Dialogs) { Title = "Q1" };
        }

        public Conn Connection { get; } = new();
        public History History { get; } = new();
        public FakeDialogService Dialogs { get; } = new();
        public StubSqlEditor Editor { get; } = new();
        public ConnectionSession Session { get; }
        public QueryTabViewModel Tab { get; }

        public async Task RunAsync(string script)
        {
            Editor.Text = script;
            await Tab.RunAllCommand.ExecuteAsync(null);
        }
    }

    [Fact]
    public async Task A_production_select_asks_nothing()
    {
        var r = new Rig();
        await r.RunAsync("select 1");
        Assert.Empty(r.Dialogs.ProductionRequests);
        Assert.Equal(["select 1"], r.Connection.Sql);
    }

    [Fact]
    public async Task A_staging_delete_asks_nothing()
    {
        var r = new Rig(ConnectionEnvironment.Staging);
        await r.RunAsync("delete from t");
        Assert.Empty(r.Dialogs.ProductionRequests);
        Assert.Single(r.Connection.Sql);
    }

    [Fact]
    public async Task A_production_delete_without_where_asks_with_the_required_text_and_the_estimate()
    {
        var r = new Rig();
        r.Dialogs.ProductionResult = new(true, false);
        await r.RunAsync("delete from t");
        var request = Assert.Single(r.Dialogs.ProductionRequests);
        Assert.Equal("shop", request.RequiredText);
        Assert.Equal(1000, request.EstimatedRows);
        Assert.Equal(ConfirmationKind.Run, request.Kind);
        Assert.Equal(["delete from t"], r.Connection.Sql);
        Assert.Equal(TransactionState.None, r.Session.Transaction.State);
    }

    [Fact]
    public async Task An_estimator_failure_still_asks_without_a_figure()
    {
        var r = new Rig();
        r.Connection.PlanFails = true;
        await r.RunAsync("delete from t");
        var request = Assert.Single(r.Dialogs.ProductionRequests);
        Assert.Null(request.EstimatedRows);
        Assert.Single(r.Connection.Sql);
    }

    [Fact]
    public async Task A_declined_run_sends_nothing_and_writes_no_history()
    {
        var r = new Rig();
        r.Dialogs.ProductionResult = new(false, true);
        await r.RunAsync("delete from t");
        Assert.Empty(r.Connection.Sql);
        Assert.Empty(r.History.Entries);
        Assert.Equal(UiText.Get("query.cancelled"), r.Tab.StatusText);
        Assert.Equal(TransactionMode.AutoCommit, r.Session.Transaction.Mode);
    }

    [Fact]
    public async Task Confirmed_with_the_option_switches_to_manual_and_opens_a_transaction()
    {
        var r = new Rig();
        r.Dialogs.ProductionResult = new(true, true);
        await r.RunAsync("delete from t");
        Assert.Equal(TransactionMode.Manual, r.Session.Transaction.Mode);
        Assert.Equal(TransactionState.Open, r.Session.Transaction.State);
        Assert.Equal(["BEGIN", "delete from t"], r.Connection.Sql);
    }

    [Fact]
    public async Task Confirmed_without_the_option_runs_auto_committed()
    {
        var r = new Rig();
        r.Dialogs.ProductionResult = new(true, false);
        await r.RunAsync("delete from t");
        Assert.Equal(TransactionMode.AutoCommit, r.Session.Transaction.Mode);
        Assert.Equal(["delete from t"], r.Connection.Sql);
    }

    [Fact]
    public async Task Manual_mode_locks_the_option()
    {
        var r = new Rig();
        await r.Session.Transaction.SetModeAsync(TransactionMode.Manual, CancellationToken.None);
        await r.RunAsync("delete from t where a = 1");
        var request = Assert.Single(r.Dialogs.ProductionRequests);
        Assert.True(request.ShowTransactionOption);
        Assert.True(request.TransactionOptionLocked);
        Assert.Equal(["BEGIN", "delete from t where a = 1"], r.Connection.Sql);
    }

    [Fact]
    public async Task A_script_of_two_statements_asks_once()
    {
        var r = new Rig();
        await r.RunAsync("insert into t values (1); insert into t values (2)");
        var request = Assert.Single(r.Dialogs.ProductionRequests);
        Assert.Equal(2, request.Statements.Count);
        Assert.Null(request.EstimatedRows);
        Assert.Equal(2, r.Connection.Sql.Count);
    }

    [Fact]
    public async Task Valider_on_production_asks_with_the_journal_and_a_decline_keeps_the_transaction()
    {
        var r = new Rig();
        await r.Session.Transaction.SetModeAsync(TransactionMode.Manual, CancellationToken.None);
        await r.RunAsync("insert into t values (1)");
        r.Dialogs.ProductionRequests.Clear();
        r.Dialogs.ProductionResult = new(false, false);

        await r.Tab.CommitCommand.ExecuteAsync(null);

        var request = Assert.Single(r.Dialogs.ProductionRequests);
        Assert.Equal(ConfirmationKind.Commit, request.Kind);
        Assert.Single(request.Statements);
        Assert.Equal(TransactionState.Open, r.Session.Transaction.State);
        Assert.DoesNotContain("COMMIT", r.Connection.Sql);

        r.Dialogs.ProductionResult = new(true, false);
        await r.Tab.CommitCommand.ExecuteAsync(null);
        Assert.Equal(TransactionState.None, r.Session.Transaction.State);
        Assert.Contains("COMMIT", r.Connection.Sql);
    }

    [Fact]
    public async Task Valider_outside_production_does_not_ask()
    {
        var r = new Rig(ConnectionEnvironment.Staging);
        await r.Session.Transaction.SetModeAsync(TransactionMode.Manual, CancellationToken.None);
        await r.RunAsync("insert into t values (1)");
        await r.Tab.CommitCommand.ExecuteAsync(null);
        Assert.Empty(r.Dialogs.ProductionRequests);
        Assert.Equal(TransactionState.None, r.Session.Transaction.State);
    }
}
