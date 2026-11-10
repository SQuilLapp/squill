using System.Globalization;
using Squill.Core;
using Squill.Core.Plans;
using Squill.Data;
using Squill.Platform.Storage;
using Squill.Presentation;
using Squill.Presentation.Connections;
using Squill.Presentation.Editor;
using Squill.Presentation.Localization;
using Squill.Presentation.Plans;
using Squill.Presentation.Query;
using Squill.Presentation.Results;
using Squill.Presentation.Sessions;
using Squill.Presentation.Workspace;
using Squill.Sql.Ddl;
using Xunit;

namespace Squill.Data.Tests.Scenarios;

/// <summary>Engine-specific parts of <see cref="PlanFlowScenario"/>.</summary>
/// <param name="HasActual">The engine gives actual plans (everything but SQLite).</param>
/// <param name="SlowSql">A read-only <c>select</c> that runs for at least 20 s unless cancelled (it must pass <c>PlanEligibility</c>).</param>
/// <param name="Sigil">Prefix of a named parameter in the engine: <c>:</c> for SQLite, <c>@</c> for the others.</param>
/// <param name="WritingSetup">Statements creating a table <c>pf_log</c> and a function <c>pf_write()</c> that inserts into it, or <c>null</c> when the engine cannot have a writing function in a select.</param>
/// <param name="WritingSelect">A read-only looking select that writes through <see cref="WritingSetup"/>, or <c>null</c>.</param>
public sealed record PlanFlowSpec(bool HasActual, string SlowSql, string Sigil, string? WritingSetup, string? WritingSelect);

/// <summary>Drives what a user does with <c>Expliquer</c> and <c>Analyser</c> (plan tab, layout, refusals, cancel, parameters, re-run, close) through the real view models, the real provider and a live connection.</summary>
public static class PlanFlowScenario
{
    private static CancellationToken Ct => CancellationToken.None;

    /// <summary>Runs every case against one engine.</summary>
    /// <param name="provider">The provider of the engine under test.</param>
    /// <param name="profile">Profile of an empty, writable database.</param>
    /// <param name="password">The password of the profile, or <c>null</c>.</param>
    /// <param name="spec">What differs per engine.</param>
    public static async Task RunAsync(IDatabaseProvider provider, ConnectionProfile profile, string? password, PlanFlowSpec spec)
    {
        var registry = new ProviderRegistry();
        registry.Register(provider);
        var store = new ScenarioProfileStore(password);
        var dialogs = new Dialogs();
        var history = new Recorder();
        var manager = new SessionManager(registry, store, new ScenarioTunnelFactory());
        var workspace = new WorkspaceViewModel(
            manager, new ConnectionListViewModel(store, registry, dialogs), history, new ScenarioSettings(), new ImmediateDispatcher(), dialogs, () => new StubSqlEditor(), store);
        workspace.ConnectionList.Profiles.Add(profile);
        await workspace.ConnectCommand.ExecuteAsync(profile);
        var tab = Assert.Single(workspace.Tabs);
        var session = tab.Session;
        var rig = new Rig(workspace, tab, dialogs, history);
        try
        {
            var d = profile.Dialect;
            var schema = d switch { DatabaseDialect.Sqlite => "main", DatabaseDialect.PostgreSql => "public", DatabaseDialect.SqlServer => "dbo", _ => profile.Database };
            var t = SqlQuoting.QualifiedName(schema, "pf_t", d);

            await rig.RunAllAsync($"create table {t} (id integer primary key, name varchar(50)); insert into {t} (id, name) values (1, 'a'), (2, 'b'), (3, 'c')");
            var runs = history.Count;

            var planTab = await ExplainOpensAPlanTab(rig, t);
            Assert.Equal(runs, history.Count); // a plan is not a run
            await RunStillWorks(rig, t);
            await ExplainFailureOpensNoTab(rig, t);
            await RunStillWorks(rig, t);
            await RerunExplainsTheOldStatement(rig, planTab);
            await ParametersThenExplain(rig, t, spec);
            await AnalyzeCases(rig, t, spec);
            await ClosingTheQueryTabClosesThePlanTab(rig);
        }
        finally
        {
            await manager.CloseAsync(session);
        }
    }

    private static async Task<PlanTabViewModel> ExplainOpensAPlanTab(Rig rig, string t)
    {
        rig.Editor.Text = $"select 1 as one; select name from {t} where id = 2";
        rig.Editor.CaretOffset = rig.Editor.Text.Length - 3;
        Assert.True(rig.Tab.CanExplain, rig.Tab.ExplainDisabledReason);
        await rig.Tab.ExplainCommand.ExecuteAsync(null);
        var planTab = AssertOpened(rig, PlanMode.Estimated, 1);
        Assert.Contains("pf_t", planTab.Statement, StringComparison.Ordinal);
        Assert.Contains("pf_t", string.Join(' ', planTab.Plan.Plan.Flatten().Select(n => n.Target)), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UiText.Format("tab.plan", rig.Tab.Title), planTab.Title);
        return planTab;
    }

    private static PlanTabViewModel AssertOpened(Rig rig, PlanMode mode, int count)
    {
        var planTab = Assert.Single(rig.Workspace.Plans, p => p.Source == rig.Tab);
        Assert.Equal(count, rig.Workspace.Plans.Count);
        Assert.Same(planTab, rig.Workspace.ActivePlan);
        Assert.Equal(mode, planTab.Mode);
        Assert.NotEmpty(planTab.Plan.Rows);
        AssertNoOverlap(PlanLayout.Compute(planTab.Plan.Rows, s => s.Length * 7.0), planTab.Plan.Rows.Count);
        return planTab;
    }

    private static void AssertNoOverlap(PlanLayoutResult layout, int rows)
    {
        Assert.Equal(rows, layout.Nodes.Count + layout.Pills.Count);
        for (var i = 0; i < layout.Nodes.Count; i++)
        {
            for (var j = i + 1; j < layout.Nodes.Count; j++)
            {
                var a = layout.Nodes[i];
                var b = layout.Nodes[j];
                var apart = a.X + a.Width <= b.X || b.X + b.Width <= a.X || a.Y + a.Height <= b.Y || b.Y + b.Height <= a.Y;
                Assert.True(apart, $"Cards {a.Id} and {b.Id} overlap.");
            }
        }
    }

    private static async Task RunStillWorks(Rig rig, string t)
    {
        rig.Editor.Text = $"select count(*) from {t}";
        await rig.Tab.RunCommand.ExecuteAsync(null);
        var result = Assert.Single(rig.Tab.Results);
        Assert.Equal(ResultKind.Grid, result.Kind);
        Assert.Equal(3, Convert.ToInt32(result.Grid!.GetCell(0, 0), CultureInfo.InvariantCulture));
    }

    private static async Task ExplainFailureOpensNoTab(Rig rig, string t)
    {
        var before = rig.Workspace.Plans.Count;
        rig.Editor.Text = $"select * from pf_missing_table where id in (select id from {t})";
        await rig.Tab.ExplainCommand.ExecuteAsync(null);
        Assert.Equal(before, rig.Workspace.Plans.Count);
        Assert.Contains("pf_missing_table", rig.Tab.StatusText, StringComparison.OrdinalIgnoreCase);
        var message = rig.Tab.Results[^1]; // the failure is appended to the results of the earlier run
        Assert.Equal(ResultKind.Message, message.Kind);
        Assert.Contains("pf_missing_table", message.Text, StringComparison.OrdinalIgnoreCase);
        Assert.False(rig.Tab.IsRunning);
    }

    private static async Task RerunExplainsTheOldStatement(Rig rig, PlanTabViewModel planTab)
    {
        var statement = planTab.Statement;
        rig.Editor.Text = "select 1 as edited";
        await planTab.RerunCommand.ExecuteAsync(null);
        Assert.Equal(statement, planTab.Statement);
        Assert.Equal("", planTab.MessageLine);
        Assert.Contains("pf_t", string.Join(' ', planTab.Plan.Plan.Flatten().Select(n => n.Target)), StringComparison.OrdinalIgnoreCase);
        Assert.Single(rig.Workspace.Plans);
    }

    private static async Task ParametersThenExplain(Rig rig, string t, PlanFlowSpec spec)
    {
        rig.Dialogs.Answer = names => [.. names.Select(n => new ParameterValue(n, "c", false))];
        rig.Editor.Text = $"select id from {t} where name = {spec.Sigil}who";
        await rig.Tab.ExplainCommand.ExecuteAsync(null);
        Assert.Equal(["who"], rig.Dialogs.AskedNames);
        // The engine explains the statement with its parameter, or the provider says so (reason "parameters"): never a silent substitution.
        var planTab = rig.Workspace.Plans.Single();
        Assert.True(
            rig.Tab.StatusText == UiText.Get("plan.unavailable.parameters") || planTab.Plan.StatusLine == rig.Tab.StatusText,
            $"Status: {rig.Tab.StatusText}; statement: {planTab.Statement}");
    }

    private static async Task AnalyzeCases(Rig rig, string t, PlanFlowSpec spec)
    {
        if (!spec.HasActual)
        {
            rig.Editor.Text = $"select name from {t}";
            Assert.False(rig.Tab.CanAnalyze);
            Assert.Equal(UiText.Get(PlanMessageCodes.ReasonNoAnalyze), rig.Tab.AnalyzeDisabledReason);
            await rig.Tab.AnalyzeCommand.ExecuteAsync(null);
            Assert.Equal(rig.Tab.AnalyzeDisabledReason, rig.Tab.StatusText);
            return;
        }

        rig.Editor.Text = $"select name from {t} order by name";
        Assert.True(rig.Tab.CanAnalyze, rig.Tab.AnalyzeDisabledReason);
        await rig.Tab.AnalyzeCommand.ExecuteAsync(null);
        AssertOpened(rig, PlanMode.Actual, 1);
        await RunStillWorks(rig, t);

        // An update is refused, never executed.
        rig.Editor.Text = $"update {t} set name = 'zzz'";
        Assert.False(rig.Tab.CanAnalyze);
        await rig.Tab.AnalyzeCommand.ExecuteAsync(null);
        Assert.Equal(rig.Tab.AnalyzeDisabledReason, rig.Tab.StatusText);
        Assert.NotEqual("", rig.Tab.AnalyzeDisabledReason);
        await AssertCountAsync(rig, $"select count(*) from {t} where name = 'zzz'", 0);

        if (spec.WritingSelect is { } writing)
        {
            // Read-only transaction: the function fails instead of writing.
            await rig.RunAllAsync(spec.WritingSetup!);
            rig.Editor.Text = writing;
            await rig.Tab.AnalyzeCommand.ExecuteAsync(null);
            await AssertCountAsync(rig, "select count(*) from pf_log", 0);
        }

        // Cancelled analyze, then a normal run on the same connection.
        rig.Editor.Text = spec.SlowSql;
        var analyze = rig.Tab.AnalyzeCommand.ExecuteAsync(null);
        for (var i = 0; i < 50 && !rig.Tab.IsRunning; i++)
            await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.True(rig.Tab.IsRunning);
        await Task.Delay(500, TestContext.Current.CancellationToken);
        rig.Tab.CancelCommand.Execute(null);
        await analyze.WaitAsync(TimeSpan.FromSeconds(15), Ct);
        Assert.Equal(UiText.Get("query.cancelled"), rig.Tab.StatusText);
        Assert.False(rig.Tab.IsRunning);
        await RunStillWorks(rig, t);
    }

    private static async Task AssertCountAsync(Rig rig, string sql, int expected)
    {
        rig.Editor.Text = sql;
        await rig.Tab.RunCommand.ExecuteAsync(null);
        var result = Assert.Single(rig.Tab.Results);
        Assert.Equal(expected, Convert.ToInt32(result.Grid!.GetCell(0, 0), CultureInfo.InvariantCulture));
    }

    private static async Task ClosingTheQueryTabClosesThePlanTab(Rig rig)
    {
        Assert.NotEmpty(rig.Workspace.Plans);
        await rig.Workspace.CloseTabCommand.ExecuteAsync(rig.Tab);
        Assert.Empty(rig.Workspace.Plans);
        Assert.Null(rig.Workspace.ActivePlan);
    }

    private sealed class Rig(WorkspaceViewModel workspace, QueryTabViewModel tab, Dialogs dialogs, Recorder history)
    {
        public WorkspaceViewModel Workspace { get; } = workspace;

        public QueryTabViewModel Tab { get; } = tab;

        public StubSqlEditor Editor => (StubSqlEditor)Tab.Editor;

        public Dialogs Dialogs { get; } = dialogs;

        public Recorder History { get; } = history;

        public async Task RunAllAsync(string script)
        {
            Editor.Text = script;
            await Tab.RunAllCommand.ExecuteAsync(null);
            Assert.False(Tab.Results.Any(r => r.Kind == ResultKind.Message && r.Text.Contains("rror", StringComparison.Ordinal)), string.Join(" | ", Tab.Results.Select(r => r.Text)));
        }
    }

    private sealed class Recorder : List<HistoryEntry>, IQueryHistoryStore
    {
        public ValueTask AppendAsync(HistoryEntry e, CancellationToken ct)
        {
            Add(e);
            return ValueTask.CompletedTask;
        }

        public ValueTask<IReadOnlyList<HistoryEntry>> ListAsync(int max, CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<HistoryEntry>>([.. this]);

        public ValueTask ClearAsync(CancellationToken ct)
        {
            Clear();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Dialogs : IDialogService
    {
        public Func<IReadOnlyList<string>, IReadOnlyList<ParameterValue>?> Answer { get; set; } = _ => null;

        public IReadOnlyList<string> AskedNames { get; private set; } = [];

        public ValueTask<bool> ConfirmAsync(string title, string message) => ValueTask.FromResult(true);

        public ValueTask<string?> PromptAsync(string title, string message, string? initial) => ValueTask.FromResult(initial);

        public ValueTask<string?> PickSaveFileAsync(string suggestedName, string extension) => ValueTask.FromResult<string?>(null);

        public ValueTask ShowErrorAsync(string title, string message) => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<ParameterValue>?> PromptParametersAsync(IReadOnlyList<string> names, IReadOnlyDictionary<string, ParameterValue> defaults)
        {
            AskedNames = names;
            return ValueTask.FromResult(Answer(names));
        }
    }
}
