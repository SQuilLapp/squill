using Squill.Core;
using Squill.Core.Plans;
using Squill.Data;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation.Editor;
using Squill.Presentation.Localization;
using Squill.Presentation.Plans;
using Squill.Presentation.Query;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.Presentation.Tests.Plans;

public sealed class PlanTabTests
{
    private sealed class PlanConnection : IDatabaseConnection, IPlanReader
    {
        public List<(string Sql, PlanMode Mode)> Calls { get; } = [];
        public Exception? Failure { get; set; }
        public Func<CancellationToken, Task>? Gate { get; set; }
        public bool Actual { get; set; } = true;
        public string ServerVersion => "9.9";
        public bool SupportsActualPlan => Actual;

        public async ValueTask<QueryPlan> ExplainAsync(string sql, IReadOnlyList<QueryParameter> parameters, PlanMode mode, CancellationToken cancellationToken)
        {
            Calls.Add((sql, mode));
            if (Gate is { } gate)
                await gate(cancellationToken);
            return Failure is { } ex ? throw ex : MakePlan(sql, mode);
        }

        public ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<string>>([]);

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => ValueTask.FromResult(SchemaSnapshot.Empty);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static QueryPlan MakePlan(string sql, PlanMode mode) => new(
        DatabaseDialect.PostgreSql, mode, sql,
        PlanIds.Assign(new PlanNode(0, "Seq Scan", PlanNodeKind.TableScan, "t", null, 5, 1, null, null, null, null, null, [], [], false, [])),
        [], [], null, null, null, false, PlanSourceFormat.Json, "{}");

    private sealed class History : IQueryHistoryStore
    {
        public int Appends { get; private set; }

        public ValueTask AppendAsync(HistoryEntry e, CancellationToken ct)
        {
            Appends++;
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

    private sealed class Fixture
    {
        public Fixture(string script, ConnectionEnvironment environment = ConnectionEnvironment.None)
        {
            Editor = new StubSqlEditor { Text = script };
            Session = new ConnectionSession(
                new ConnectionProfile(Guid.NewGuid(), "p", DatabaseDialect.PostgreSql, "h", 1, "d", "u", null, null, environment),
                Connection, SchemaSnapshot.Empty, null);
            var session = Session;
            Vm = new QueryTabViewModel(session, Editor, History, new Settings(), new ImmediateDispatcher(), Dialogs) { Title = "Q1" };
            Vm.PlanReady += p => Opened.Add(p);
        }

        public ConnectionSession Session { get; }
        public PlanConnection Connection { get; } = new();
        public FakeDialogService Dialogs { get; } = new();
        public StubSqlEditor Editor { get; }
        public History History { get; } = new();
        public QueryTabViewModel Vm { get; }
        public List<PlanTabViewModel> Opened { get; } = [];
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Plans_AreRefusedOnAFailedTransaction_AndRunInsideAnOpenOne(bool failed)
    {
        var f = new Fixture("select 1");
        var tx = f.Session.Transaction;
        tx.ObserveSuccess("BEGIN", Squill.Sql.Transactions.StatementClassifier.Classify("BEGIN", DatabaseDialect.PostgreSql), -1, "Q1");
        if (failed)
            await tx.ObserveFailureAsync(new InvalidOperationException(), Squill.Sql.Transactions.StatementClassifier.Classify("update t set a = 1", DatabaseDialect.PostgreSql), CancellationToken.None);

        await f.Vm.ExplainCommand.ExecuteAsync(null);
        await f.Vm.AnalyzeCommand.ExecuteAsync(null);

        Assert.Equal(failed ? 0 : 2, f.Connection.Calls.Count);
        if (failed)
            Assert.Equal(UiText.Get("tx.failed"), f.Vm.StatusText);
        Assert.False(tx.IsBusy);
    }

    [Fact]
    public async Task Explain_UsesTheStatementUnderTheCaret_AndOpensOnePlanTab()
    {
        var f = new Fixture("select 1; select 2;");
        f.Editor.CaretOffset = 15;

        await f.Vm.ExplainCommand.ExecuteAsync(null);
        await f.Vm.ExplainCommand.ExecuteAsync(null);

        Assert.Equal([("select 2;", PlanMode.Estimated), ("select 2;", PlanMode.Estimated)], f.Connection.Calls.Select(c => (c.Sql.Trim(), c.Mode)));
        Assert.Equal(2, f.Opened.Count);
        Assert.Same(f.Opened[0], f.Opened[1]);
        Assert.Equal("Q1 · Plan", f.Opened[0].Title);
        Assert.Equal(0, f.History.Appends);
        Assert.False(f.Vm.IsRunning);
        f.Vm.Title = "Renamed";
        Assert.Equal("Renamed · Plan", f.Opened[0].Title);
    }

    [Fact]
    public async Task OpenInEditor_RaisesTheScriptOfTheSourceTab()
    {
        var f = new Fixture("select 1");
        var scripts = new List<string>();
        f.Vm.ScriptRequested += scripts.Add;
        await f.Vm.ExplainCommand.ExecuteAsync(null);

        f.Opened[0].OpenInEditorCommand.Execute("CREATE INDEX x ON t (a);");
        f.Opened[0].OpenInEditorCommand.Execute("");

        Assert.Equal(["CREATE INDEX x ON t (a);"], scripts);
    }

    [Fact]
    public async Task Analyze_WriteStatement_IsRefusedWithTheReason()
    {
        var f = new Fixture("delete from t");

        Assert.False(f.Vm.CanAnalyze);
        Assert.True(f.Vm.CanExplain);
        await f.Vm.AnalyzeCommand.ExecuteAsync(null);

        Assert.Empty(f.Connection.Calls);
        Assert.Equal(UiText.Get("plan.reason.writes"), f.Vm.StatusText);
        Assert.Equal(UiText.Get("plan.reason.writes"), f.Vm.AnalyzeDisabledReason);
    }

    [Fact]
    public async Task Analyze_WhenTheEngineHasNoActualPlan_IsDisabled()
    {
        var f = new Fixture("select 1");
        f.Connection.Actual = false;
        f.Editor.Text = "select 2";

        Assert.False(f.Vm.CanAnalyze);
        Assert.Equal(UiText.Get("plan.reason.noanalyze"), f.Vm.AnalyzeDisabledReason);
        await f.Vm.AnalyzeCommand.ExecuteAsync(null);
        Assert.Empty(f.Connection.Calls);
        Assert.True(f.Vm.CanExplain);
    }

    [Fact]
    public async Task Analyze_OnProduction_AsksFirst()
    {
        var f = new Fixture("select 1", ConnectionEnvironment.Production);
        f.Dialogs.ConfirmResult = false;

        await f.Vm.AnalyzeCommand.ExecuteAsync(null);
        Assert.Empty(f.Connection.Calls);
        Assert.Single(f.Dialogs.Confirms);

        f.Dialogs.ConfirmResult = true;
        await f.Vm.AnalyzeCommand.ExecuteAsync(null);
        Assert.Single(f.Connection.Calls);
        Assert.Equal(PlanMode.Actual, f.Connection.Calls[0].Mode);

        await f.Vm.ExplainCommand.ExecuteAsync(null);
        Assert.Equal(2, f.Dialogs.Confirms.Count);
    }

    [Fact]
    public async Task ParseFailure_OpensTextModeWithTheRawOutput()
    {
        var f = new Fixture("select 1");
        f.Connection.Failure = new PlanParseException("Unreadable plan", "<raw/>");

        await f.Vm.ExplainCommand.ExecuteAsync(null);

        var tab = Assert.Single(f.Opened);
        Assert.Equal(PlanViewMode.Text, tab.Plan.ViewMode);
        Assert.Equal("<raw/>", tab.Plan.SourceText);
        Assert.Equal("Unreadable plan", tab.MessageLine);
    }

    [Fact]
    public async Task Unavailable_ShowsTheReasonInTheStatusLine()
    {
        var f = new Fixture("select 1");
        f.Connection.Failure = new PlanUnavailableException("parameters", "x");

        await f.Vm.ExplainCommand.ExecuteAsync(null);

        Assert.Empty(f.Opened);
        Assert.Equal(UiText.Get("plan.unavailable.parameters"), f.Vm.StatusText);
    }

    [Fact]
    public async Task ServerError_ShowsAMessageResult_AndNoPlanTab()
    {
        var f = new Fixture("select 1");
        f.Connection.Failure = new InvalidOperationException("syntax error");

        await f.Vm.ExplainCommand.ExecuteAsync(null);

        Assert.Empty(f.Opened);
        Assert.Equal("syntax error", Assert.Single(f.Vm.Results).Text);
        Assert.False(f.Vm.IsRunning);
    }

    [Fact]
    public async Task Rerun_UsesTheStoredStatement_NotTheEditorText()
    {
        var f = new Fixture("select 1");
        await f.Vm.ExplainCommand.ExecuteAsync(null);
        f.Editor.Text = "select 99";

        await f.Opened[0].RerunCommand.ExecuteAsync(null);

        Assert.Equal(2, f.Connection.Calls.Count);
        Assert.Equal("select 1", f.Connection.Calls[1].Sql);
        Assert.False(f.Opened[0].IsBusy);
    }

    [Fact]
    public async Task Explain_WhileRunning_IsDisabled_AndCancelStopsIt()
    {
        var f = new Fixture("select 1");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Connection.Gate = async ct =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
        };

        var running = f.Vm.ExplainCommand.ExecuteAsync(null);
        await entered.Task;
        Assert.True(f.Vm.IsRunning);
        Assert.False(f.Vm.CanExplain);
        Assert.False(f.Vm.CanAnalyze);

        f.Vm.CancelCommand.Execute(null);
        await running;

        Assert.Equal(UiText.Get("query.cancelled"), f.Vm.StatusText);
        Assert.Empty(f.Opened);
        Assert.True(f.Vm.CanExplain);
    }
}
