using System.Globalization;
using Squill.Core;
using Squill.Core.Plans;
using Squill.Core.Transactions;
using Squill.Data;
using Squill.Platform.Storage;
using Squill.Presentation;
using Squill.Presentation.Connections;
using Squill.Presentation.Designer;
using Squill.Presentation.Editing;
using Squill.Presentation.Editor;
using Squill.Presentation.Localization;
using Squill.Presentation.Query;
using Squill.Presentation.Results;
using Squill.Presentation.Safety;
using Squill.Presentation.Sessions;
using Squill.Presentation.Workspace;
using Squill.Sql.Transactions;
using Xunit;

namespace Squill.Data.Tests.Scenarios;

/// <summary>Engine-specific parts of <see cref="TransactionFlowScenario"/>.</summary>
/// <param name="BeginSql">The statement a user types to open a transaction (<c>BEGIN</c>, or <c>BEGIN TRANSACTION</c> on SQL Server).</param>
/// <param name="CannotRunSql">A statement the engine cannot run inside a transaction and that must not create anything when sent, or <c>null</c> when the engine has none (MySQL commits implicitly instead).</param>
/// <param name="ErrorFailsTransaction">An error inside a transaction leaves it <see cref="TransactionState.Failed"/> (PostgreSQL only).</param>
/// <param name="DdlImplicitlyCommits">DDL commits the open transaction (MySQL).</param>
/// <param name="HasEstimate">The planner gives a figure for a <c>DELETE</c> (everything but SQLite).</param>
/// <param name="CanAnalyze">The engine gives actual plans (everything but SQLite).</param>
/// <param name="RequiredText">The word a destructive statement asks to be typed on a Production profile: the database name, or the SQLite file name.</param>
public sealed record TransactionFlowSpec(string BeginSql, string? CannotRunSql, bool ErrorFailsTransaction, bool DdlImplicitlyCommits, bool HasEstimate, bool CanAnalyze, string RequiredText);

/// <summary>Drives what a user does with manual transactions and the production confirmation (toolbar commands, close, disconnect, exit) through the real view models, the real provider and live connections.</summary>
public static class TransactionFlowScenario
{
    private const string Table = "tf_t";
    private static CancellationToken Ct => CancellationToken.None;

    /// <summary>Runs every case against one engine.</summary>
    /// <param name="provider">The provider of the engine under test.</param>
    /// <param name="profile">Profile of an empty, writable database (environment none).</param>
    /// <param name="password">The password of the profile, or <c>null</c>.</param>
    /// <param name="spec">What differs per engine.</param>
    public static async Task RunAsync(IDatabaseProvider provider, ConnectionProfile profile, string? password, TransactionFlowSpec spec)
    {
        await Case(provider, profile, password, spec, CommitAndRollback);
        await Case(provider, profile, password, spec, SelectOpensNothing);
        await Case(provider, profile, password, spec, FailingScript);
        await Case(provider, profile, password, spec, TypedBeginAndRollback);
        await Case(provider, profile, password, spec, DdlInsideATransaction);
        if (spec.CannotRunSql is not null)
            await Case(provider, profile, password, spec, StatementThatCannotRunIsRefused);
        await Case(provider, profile, password, spec, GridAndDesignerInsideATransaction);
        if (spec.CanAnalyze)
            await Case(provider, profile, password, spec, AnalyzeKeepsTheTransaction);
        await Case(provider, profile, password, spec, TwoTabsShareTheTransaction);
        await Case(provider, profile, password, spec, ClosingTabDisconnectAndExit);
        await Case(provider, profile, password, spec, ClosingTheSessionRollsBack);
        await Production(provider, profile with { Id = Guid.NewGuid(), Environment = ConnectionEnvironment.Production }, password, spec);
        await Staging(provider, profile with { Id = Guid.NewGuid(), Environment = ConnectionEnvironment.Staging }, password);
    }

    private static async Task Case(IDatabaseProvider provider, ConnectionProfile profile, string? password, TransactionFlowSpec spec, Func<Rig, TransactionFlowSpec, Task> body)
    {
        await using var rig = await Rig.OpenAsync(provider, profile, password);
        await rig.ResetAsync();
        await body(rig, spec);
    }

    private static async Task CommitAndRollback(Rig rig, TransactionFlowSpec spec)
    {
        await rig.ManualAsync();
        foreach (var commit in new[] { true, false })
        {
            await rig.RunAsync($"insert into {Table} (id, name) values ({(commit ? 1 : 2)}, 'x')");
            Assert.Equal(TransactionState.Open, rig.Tx.State);
            Assert.Equal(1, rig.Tx.PendingCount);
            Assert.True(rig.Tab.CanCommit && rig.Tab.CanRollback);
            await (commit ? rig.Tab.CommitCommand : rig.Tab.RollbackCommand).ExecuteAsync(null);
            Assert.Equal(TransactionState.None, rig.Tx.State);
            Assert.Equal(0, rig.Tx.PendingCount);
            Assert.Equal(commit ? 1 : 0, await rig.CountAsync($"select count(*) from {Table} where id = {(commit ? 1 : 2)}"));
            Assert.Contains(rig.History, e => e.Sql == (commit ? "COMMIT" : "ROLLBACK") && e.Succeeded);
        }

        Assert.Equal(TransactionMode.Manual, rig.Tx.Mode);
    }

    private static async Task SelectOpensNothing(Rig rig, TransactionFlowSpec spec)
    {
        await rig.ManualAsync();
        await rig.RunAsync($"select * from {Table}");
        Assert.Equal(TransactionState.None, rig.Tx.State);
        Assert.False(rig.Tab.CanCommit || rig.Tab.CanRollback);
    }

    private static async Task FailingScript(Rig rig, TransactionFlowSpec spec)
    {
        await rig.ManualAsync();
        await rig.RunAsync($"insert into {Table} (id, name) values (1, 'a'); insert into {Table} (id, name) values (1, 'dup'); insert into {Table} (id, name) values (3, 'c')");
        Assert.Equal(spec.ErrorFailsTransaction ? TransactionState.Failed : TransactionState.Open, rig.Tx.State);
        Assert.Equal(1, rig.Tx.PendingCount); // the failed statement is not counted, the third never ran
        if (spec.ErrorFailsTransaction)
        {
            await rig.RunAsync("select 1");
            Assert.Equal(UiText.Get("tx.failed"), rig.Tab.Results[^1].Text);
            Assert.False(rig.Tab.CanCommit);
            Assert.True(rig.Tab.CanRollback);
        }
        else
        {
            await rig.RunAsync($"insert into {Table} (id, name) values (3, 'c')");
            Assert.Equal(2, rig.Tx.PendingCount);
        }

        await rig.Tab.RollbackCommand.ExecuteAsync(null);
        Assert.Equal(TransactionState.None, rig.Tx.State);
        Assert.Equal(0, rig.Tx.PendingCount);
        Assert.Equal(0, await rig.CountAsync($"select count(*) from {Table}"));
    }

    private static async Task TypedBeginAndRollback(Rig rig, TransactionFlowSpec spec)
    {
        Assert.Equal(TransactionMode.AutoCommit, rig.Tx.Mode);
        await rig.RunAsync(spec.BeginSql);
        Assert.Equal(TransactionState.Open, rig.Tx.State);
        Assert.Equal(TransactionMode.AutoCommit, rig.Tx.Mode);
        Assert.Equal(0, rig.Tx.PendingCount);
        await rig.RunAsync($"insert into {Table} (id, name) values (1, 'a')");
        Assert.Equal(1, rig.Tx.PendingCount);
        await rig.RunAsync("rollback");
        Assert.Equal(TransactionState.None, rig.Tx.State);
        Assert.Equal(0, await rig.CountAsync($"select count(*) from {Table}"));

        await rig.RunAsync(spec.BeginSql);
        await rig.RunAsync($"insert into {Table} (id, name) values (1, 'a')");
        await rig.RunAsync("commit");
        Assert.Equal(TransactionState.None, rig.Tx.State);
        Assert.Equal(1, await rig.CountAsync($"select count(*) from {Table}"));
    }

    private static async Task DdlInsideATransaction(Rig rig, TransactionFlowSpec spec)
    {
        await rig.ManualAsync();
        await rig.RunAsync($"insert into {Table} (id, name) values (1, 'a')");
        const string Ddl = "create table tf_ddl (id integer primary key)";
        if (spec.DdlImplicitlyCommits)
        {
            rig.Dialogs.Confirm = false;
            await rig.RunAsync(Ddl);
            Assert.Equal(TransactionState.Open, rig.Tx.State); // declined: nothing sent
            Assert.Equal(1, rig.Tx.PendingCount);
            Assert.False(await rig.ExistsAsync("tf_ddl"));
            Assert.Equal(UiText.Get("query.cancelled"), rig.Tab.StatusText);

            rig.Dialogs.Confirm = true;
            await rig.RunAsync(Ddl);
            Assert.Equal(TransactionState.None, rig.Tx.State);
            Assert.True(await rig.ExistsAsync("tf_ddl"));
            Assert.Equal(1, await rig.CountAsync($"select count(*) from {Table}")); // the earlier insert was committed
        }
        else
        {
            await rig.RunAsync(Ddl);
            Assert.Equal(TransactionState.Open, rig.Tx.State);
            Assert.Equal(2, rig.Tx.PendingCount);
            await rig.Tab.RollbackCommand.ExecuteAsync(null);
            Assert.False(await rig.ExistsAsync("tf_ddl"));
            Assert.Equal(0, await rig.CountAsync($"select count(*) from {Table}"));
        }

        Assert.Empty(rig.Dialogs.ProductionRequests);
        await rig.Other.ExecAsync("drop table if exists tf_ddl");
    }

    private static async Task StatementThatCannotRunIsRefused(Rig rig, TransactionFlowSpec spec)
    {
        await rig.ManualAsync();
        await rig.RunAsync($"insert into {Table} (id, name) values (1, 'a')");
        await rig.RunAsync(spec.CannotRunSql!);
        var kind = StatementClassifier.Classify(spec.CannotRunSql!, rig.Profile.Dialect).Kind.ToString().ToUpperInvariant();
        Assert.Equal(UiText.Format("tx.cannotRun", kind), rig.Tab.Results[^1].Text);
        Assert.Equal(TransactionState.Open, rig.Tx.State); // a statement that was sent would have failed the transaction
        Assert.Equal(1, rig.Tx.PendingCount);
        await rig.Tab.RollbackCommand.ExecuteAsync(null);
        Assert.Equal(0, await rig.CountAsync($"select count(*) from {Table}"));
    }

    private static async Task GridAndDesignerInsideATransaction(Rig rig, TransactionFlowSpec spec)
    {
        await rig.ManualAsync();
        await rig.RunAsync($"insert into {Table} (id, name) values (10, 'earlier')");
        await rig.RunAsync($"select * from {Table}");
        var ed = rig.Tab.Results.Select(r => r.Editor).First(e => e is not null)!;
        var row = ed.AddRow();
        Assert.Null(ed.TrySetCell(row, Column(ed, "id"), "11"));
        Assert.Null(ed.TrySetCell(row, Column(ed, "name"), "grid"));
        await ed.CommitCommand.ExecuteAsync(null);
        Assert.Equal(TransactionState.Open, rig.Tx.State);
        Assert.Equal(2, rig.Tx.PendingCount);
        await rig.Tab.RollbackCommand.ExecuteAsync(null);
        Assert.Equal(0, await rig.CountAsync($"select count(*) from {Table}")); // the user's insert and the grid edit are both gone

        if (spec.DdlImplicitlyCommits)
            return; // the designer apply is DDL: it asks first (DdlInsideATransaction)
        await rig.RunAsync($"insert into {Table} (id, name) values (10, 'earlier')");
        var designer = new TableDesignerViewModel(rig.Session, null, rig.Dialogs, new ImmediateDispatcher()) { Name = "tf_designed" };
        await designer.ApplyCommand.ExecuteAsync(null);
        Assert.Equal(TransactionState.Open, rig.Tx.State);
        Assert.Equal(2, rig.Tx.PendingCount);
        await rig.Tab.RollbackCommand.ExecuteAsync(null);
        Assert.False(await rig.ExistsAsync("tf_designed"));
        Assert.Equal(0, await rig.CountAsync($"select count(*) from {Table}"));
    }

    private static int Column(ResultEditorViewModel ed, string name)
    {
        for (var i = 0; i < ed.Grid.Columns.Count; i++)
        {
            if (string.Equals(ed.Grid.Columns[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        throw new Xunit.Sdk.XunitException($"no column {name}");
    }

    private static async Task AnalyzeKeepsTheTransaction(Rig rig, TransactionFlowSpec spec)
    {
        await rig.ManualAsync();
        await rig.RunAsync($"insert into {Table} (id, name) values (1, 'a')");
        rig.Editor.Text = $"select * from {Table}";
        await rig.Tab.AnalyzeCommand.ExecuteAsync(null);
        Assert.Single(rig.Workspace.Plans);
        Assert.Equal(TransactionState.Open, rig.Tx.State);
        Assert.Equal(1, rig.Tx.PendingCount);
        await rig.Tab.RollbackCommand.ExecuteAsync(null);
        Assert.Equal(0, await rig.CountAsync($"select count(*) from {Table}"));
    }

    private static async Task TwoTabsShareTheTransaction(Rig rig, TransactionFlowSpec spec)
    {
        await rig.ManualAsync();
        rig.Workspace.NewTabCommand.Execute(null);
        var tabB = rig.Workspace.Tabs[^1];
        Assert.NotSame(rig.Tab, tabB);
        ((StubSqlEditor)tabB.Editor).Text = $"insert into {Table} (id, name) values (1, 'from b')";
        await tabB.RunAllCommand.ExecuteAsync(null);
        Assert.Equal(TransactionState.Open, rig.Tx.State);
        Assert.True(rig.Tab.CanRollback); // tab A mirrors the session
        Assert.Equal(tabB.Title, rig.Tx.Journal[0].Tab);
        await rig.Tab.RollbackCommand.ExecuteAsync(null);
        Assert.Equal(TransactionState.None, tabB.Transaction.State);
        Assert.Equal(0, await rig.CountAsync($"select count(*) from {Table}"));
    }

    private static async Task ClosingTabDisconnectAndExit(Rig rig, TransactionFlowSpec spec)
    {
        // Closing the last tab: Retour keeps everything, Annuler rolls back.
        await rig.ManualAsync();
        var first = rig.Tab;
        await rig.RunAsync($"insert into {Table} (id, name) values (1, 'a')");
        rig.Dialogs.Choice = TransactionChoice.Cancel;
        await rig.Workspace.CloseTabCommand.ExecuteAsync(first);
        Assert.Contains(first, rig.Workspace.Tabs);
        Assert.Equal(TransactionState.Open, rig.Tx.State);
        Assert.Equal(1, Assert.Single(rig.Dialogs.Asked).PendingCount);
        rig.Dialogs.Choice = TransactionChoice.Rollback;
        await rig.Workspace.CloseTabCommand.ExecuteAsync(first);
        Assert.DoesNotContain(first, rig.Workspace.Tabs);
        Assert.Equal(TransactionState.None, rig.Tx.State);
        Assert.Equal(0, await rig.CountAsync($"select count(*) from {Table}"));
        Assert.Contains(rig.History, e => e.Sql == "ROLLBACK");

        // Switching to auto-commit asks too.
        rig.Workspace.NewTabCommand.Execute(null);
        var tab = rig.Workspace.Tabs[^1];
        await tab.SetModeCommand.ExecuteAsync(TransactionMode.Manual);
        ((StubSqlEditor)tab.Editor).Text = $"insert into {Table} (id, name) values (1, 'a')";
        await tab.RunAllCommand.ExecuteAsync(null);
        rig.Dialogs.Choice = TransactionChoice.Cancel;
        await tab.SetModeCommand.ExecuteAsync(TransactionMode.AutoCommit);
        Assert.Equal(TransactionMode.Manual, rig.Tx.Mode);
        Assert.Equal(TransactionState.Open, rig.Tx.State);

        // Quitting: Retour keeps it open, Valider commits.
        Assert.False(await rig.Workspace.PrepareExitAsync(Ct));
        Assert.Equal(TransactionState.Open, rig.Tx.State);
        rig.Dialogs.Choice = TransactionChoice.Commit;
        Assert.True(await rig.Workspace.PrepareExitAsync(Ct));
        Assert.Equal(TransactionState.None, rig.Tx.State);
        Assert.Equal(1, await rig.CountAsync($"select count(*) from {Table}"));
        Assert.Contains(rig.History, e => e.Sql == "COMMIT");

        // Disconnecting: Retour keeps the session, Annuler rolls back and disconnects.
        ((StubSqlEditor)tab.Editor).Text = $"insert into {Table} (id, name) values (2, 'b')";
        await tab.RunAllCommand.ExecuteAsync(null);
        rig.Dialogs.Choice = TransactionChoice.Cancel;
        await rig.Workspace.DisconnectCommand.ExecuteAsync(null);
        Assert.Single(rig.Workspace.Sessions);
        Assert.Equal(TransactionState.Open, rig.Tx.State);
        rig.Dialogs.Choice = TransactionChoice.Rollback;
        await rig.Workspace.DisconnectCommand.ExecuteAsync(null);
        Assert.Empty(rig.Workspace.Sessions);
        Assert.Equal(1, await rig.CountAsync($"select count(*) from {Table}"));
    }

    private static async Task ClosingTheSessionRollsBack(Rig rig, TransactionFlowSpec spec)
    {
        await rig.ManualAsync();
        await rig.RunAsync($"insert into {Table} (id, name) values (1, 'a')");
        await rig.Manager.CloseAsync(rig.Session);
        Assert.Equal(0, await rig.CountAsync($"select count(*) from {Table}"));
    }

    private static async Task Production(IDatabaseProvider provider, ConnectionProfile profile, string? password, TransactionFlowSpec spec)
    {
        await using var rig = await Rig.OpenAsync(provider, profile, password);
        await rig.ResetAsync();
        await rig.Other.ExecAsync($"insert into {Table} (id, name) values (1, 'a'), (2, 'b'), (3, 'c')");
        var dialogs = rig.Dialogs;

        // Declined: nothing is sent.
        dialogs.Production = _ => new ProductionConfirmationResult(false, false);
        await rig.RunAsync($"delete from {Table}");
        var request = Assert.Single(dialogs.ProductionRequests);
        Assert.Equal(ConfirmationKind.Run, request.Kind);
        Assert.Equal(spec.RequiredText, request.RequiredText);
        Assert.Equal(spec.HasEstimate, request.EstimatedRows is not null);
        Assert.True(request.ShowTransactionOption);
        Assert.Equal(3, await rig.CountAsync($"select count(*) from {Table}"));
        Assert.Equal(TransactionState.None, rig.Tx.State);

        // Accepted with the option: the session switches to manual and the statement waits for Valider.
        dialogs.ProductionRequests.Clear();
        dialogs.Production = _ => new ProductionConfirmationResult(true, true);
        await rig.RunAsync($"delete from {Table}");
        Assert.Equal(TransactionMode.Manual, rig.Tx.Mode);
        Assert.Equal(TransactionState.Open, rig.Tx.State);
        Assert.Equal(1, rig.Tx.PendingCount);
        Assert.Single(dialogs.ProductionRequests);

        // Valider asks again, declined keeps everything, accepted commits.
        dialogs.Production = _ => new ProductionConfirmationResult(false, false);
        await rig.Tab.CommitCommand.ExecuteAsync(null);
        Assert.Equal(ConfirmationKind.Commit, dialogs.ProductionRequests[^1].Kind);
        Assert.Equal(TransactionState.Open, rig.Tx.State);
        dialogs.Production = _ => new ProductionConfirmationResult(true, false);
        await rig.Tab.CommitCommand.ExecuteAsync(null);
        Assert.Equal(TransactionState.None, rig.Tx.State);
        Assert.Equal(0, await rig.CountAsync($"select count(*) from {Table}"));

        // A read-only statement never asks.
        dialogs.ProductionRequests.Clear();
        await rig.RunAsync($"select * from {Table}");
        Assert.Empty(dialogs.ProductionRequests);
    }

    private static async Task Staging(IDatabaseProvider provider, ConnectionProfile profile, string? password)
    {
        await using var rig = await Rig.OpenAsync(provider, profile, password);
        await rig.ResetAsync();
        await rig.Other.ExecAsync($"insert into {Table} (id, name) values (1, 'a')");
        await rig.RunAsync($"delete from {Table}");
        Assert.Empty(rig.Dialogs.ProductionRequests);
        Assert.Equal(0, await rig.CountAsync($"select count(*) from {Table}"));
    }

    private sealed class Rig : IAsyncDisposable
    {
        private Rig(SessionManager manager, WorkspaceViewModel workspace, Dialogs dialogs, Recorder history, ConnectionProfile profile, Connection other)
        {
            Session = workspace.Tabs[0].Session;
            Manager = manager;
            Workspace = workspace;
            Dialogs = dialogs;
            History = history;
            Profile = profile;
            Other = other;
        }

        public SessionManager Manager { get; }

        public WorkspaceViewModel Workspace { get; }

        public Dialogs Dialogs { get; }

        public Recorder History { get; }

        public ConnectionProfile Profile { get; }

        public Connection Other { get; }

        public QueryTabViewModel Tab => Workspace.Tabs[0];

        public ConnectionSession Session { get; }

        public SessionTransaction Tx => Session.Transaction;

        public StubSqlEditor Editor => (StubSqlEditor)Tab.Editor;

        public static async Task<Rig> OpenAsync(IDatabaseProvider provider, ConnectionProfile profile, string? password)
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
            var other = new Connection(await provider.OpenAsync(profile, password, Ct));
            return new Rig(manager, workspace, dialogs, history, profile, other);
        }

        // A fresh table, created on the second connection so the tab's session holds nothing.
        public async Task ResetAsync()
        {
            foreach (var name in new[] { Table, "tf_ddl", "tf_designed" })
                await Other.ExecAsync($"drop table if exists {name}");
            await Other.ExecAsync($"create table {Table} (id integer primary key, name varchar(50))");
            await Session.RefreshSchemaAsync(Ct);
        }

        public Task ManualAsync() => Tab.SetModeCommand.ExecuteAsync(TransactionMode.Manual);

        public async Task RunAsync(string script)
        {
            Editor.Text = script;
            await Tab.RunAllCommand.ExecuteAsync(null);
        }

        public Task<int> CountAsync(string sql) => Other.CountAsync(sql);

        public async Task<bool> ExistsAsync(string table)
        {
            try
            {
                await Other.CountAsync($"select count(*) from {table}");
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var session in Manager.Sessions.ToList())
                await Manager.CloseAsync(session);
            await Other.DisposeAsync();
        }
    }

    // The second connection: what any other client of the server sees.
    private sealed class Connection(IDatabaseConnection inner) : IAsyncDisposable
    {
        public async Task ExecAsync(string sql)
        {
            await using var reader = await inner.ExecuteAsync(sql, [], Ct);
            await reader.ReadAllAsync(10, Ct);
        }

        public async Task<int> CountAsync(string sql)
        {
            await using var reader = await inner.ExecuteAsync(sql, [], Ct);
            var rows = await reader.ReadAllAsync(10, Ct);
            return Convert.ToInt32(rows[0][0], CultureInfo.InvariantCulture);
        }

        public ValueTask DisposeAsync() => inner.DisposeAsync();
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
        public bool Confirm { get; set; } = true;

        public TransactionChoice Choice { get; set; } = TransactionChoice.Cancel;

        public Func<ProductionConfirmationRequest, ProductionConfirmationResult> Production { get; set; } = _ => new ProductionConfirmationResult(true, false);

        public List<ProductionConfirmationRequest> ProductionRequests { get; } = [];

        public List<TransactionCloseRequest> Asked { get; } = [];

        public ValueTask<ProductionConfirmationResult> ConfirmProductionAsync(ProductionConfirmationRequest request)
        {
            ProductionRequests.Add(request);
            return ValueTask.FromResult(Production(request));
        }

        public ValueTask<TransactionChoice> AskTransactionAsync(TransactionCloseRequest request)
        {
            Asked.Add(request);
            return ValueTask.FromResult(Choice);
        }

        public ValueTask<bool> ConfirmAsync(string title, string message) => ValueTask.FromResult(Confirm);

        public ValueTask<string?> PromptAsync(string title, string message, string? initial) => ValueTask.FromResult(initial);

        public ValueTask<string?> PickSaveFileAsync(string suggestedName, string extension) => ValueTask.FromResult<string?>(null);

        public ValueTask ShowErrorAsync(string title, string message) => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<ParameterValue>?> PromptParametersAsync(IReadOnlyList<string> names, IReadOnlyDictionary<string, ParameterValue> defaults) =>
            ValueTask.FromResult<IReadOnlyList<ParameterValue>?>(null);
    }
}
