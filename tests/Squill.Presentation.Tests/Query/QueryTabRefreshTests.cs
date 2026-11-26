using Squill.Core;
using Squill.Data;
using Squill.Data.Sqlite;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation.Editor;
using Squill.Presentation.Query;
using Squill.Presentation.Results;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.Presentation.Tests.Query;

public sealed class QueryTabRefreshTests : IAsyncLifetime
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"squill-{Guid.NewGuid():N}.db");
    private readonly FakeDialogService _dialogs = new();
    private readonly History _history = new();
    private readonly GatedConnection _gated = new();
    private StubSqlEditor _editor = null!;
    private QueryTabViewModel _tab = null!;

    public async ValueTask InitializeAsync()
    {
        var profile = new ConnectionProfile(Guid.NewGuid(), "p", DatabaseDialect.Sqlite, null, null, null, null, _file, null);
        _gated.Inner = await new SqliteDatabaseProvider().OpenAsync(profile, null, CancellationToken.None);
        foreach (var sql in new[]
        {
            "CREATE TABLE t (id INTEGER PRIMARY KEY, name TEXT NOT NULL)",
            "INSERT INTO t (name) VALUES ('a'), ('b'), ('c'), ('d'), ('e'), ('f'), ('g'), ('h'), ('i'), ('j')",
        })
            await (await _gated.Inner.ExecuteAsync(sql, [], CancellationToken.None)).DisposeAsync();
        var schema = await _gated.Inner.ReadSchemaAsync(CancellationToken.None);
        _editor = new StubSqlEditor();
        _tab = new QueryTabViewModel(new ConnectionSession(profile, _gated, schema, null), _editor, _history, new Settings(), new ImmediateDispatcher(), _dialogs);
    }

    public async ValueTask DisposeAsync()
    {
        await _gated.Inner.DisposeAsync();
        try
        {
            File.Delete(_file);
        }
        catch (IOException)
        {
        }
    }

    private async Task RunAsync(string sql)
    {
        _editor.Text = sql;
        await _tab.RunCommand.ExecuteAsync(null);
    }

    private async Task Exec(string sql) => await (await _gated.Inner.ExecuteAsync(sql, [], CancellationToken.None)).DisposeAsync();

    [Fact]
    public async Task CanRefresh_AfterSelectRun_True()
    {
        await RunAsync("SELECT * FROM t");
        Assert.True(_tab.CanRefresh);
    }

    [Fact]
    public async Task CanRefresh_AfterInsertRun_False()
    {
        await RunAsync("INSERT INTO t (name) VALUES ('z')");
        Assert.False(_tab.CanRefresh);
    }

    [Fact]
    public async Task CanRefresh_AfterFailedRun_False()
    {
        await RunAsync("SELECT * FROM missing_table");
        Assert.False(_tab.CanRefresh);
    }

    [Fact]
    public async Task Refresh_RerunsLastStatements_NotEditorText()
    {
        await RunAsync("SELECT * FROM t");
        _editor.Text = "SELECT 42";
        _gated.Executed.Clear();

        Assert.Equal(RefreshOutcome.Refreshed, await _tab.RefreshAsync(CancellationToken.None));

        Assert.Equal(["SELECT * FROM t"], _gated.Executed);
    }

    [Fact]
    public async Task Refresh_NamedParameters_NoPrompt()
    {
        _editor.Text = "SELECT * FROM t WHERE id = :id";
        var run = _tab.RunCommand.ExecuteAsync(null);
        Assert.True(_tab.IsParametersPanelOpen);
        _tab.ParameterRows[0].Text = "3";
        _tab.RunParametersCommand.Execute(null);
        await run;
        Assert.Equal(1, _tab.Results[0].Grid!.RowCount);
        _gated.Bound.Clear();

        var opened = false;
        _tab.PropertyChanged += (_, e) => opened |= e.PropertyName == nameof(QueryTabViewModel.IsParametersPanelOpen);
        Assert.Equal(RefreshOutcome.Refreshed, await _tab.RefreshAsync(CancellationToken.None));

        Assert.False(opened);
        Assert.Equal("3", Assert.Single(Assert.Single(_gated.Bound)).Value);
    }

    [Fact]
    public async Task Refresh_WritesNoHistory()
    {
        await RunAsync("SELECT * FROM t");
        await _tab.RefreshAsync(CancellationToken.None);
        await _tab.RefreshAsync(CancellationToken.None);
        Assert.Single(_history.Entries);
    }

    [Fact]
    public async Task Refresh_KeepsIsModified()
    {
        await RunAsync("SELECT * FROM t");
        _editor.Text = "SELECT 1";
        Assert.True(_tab.IsModified);
        await _tab.RefreshAsync(CancellationToken.None);
        Assert.True(_tab.IsModified);
    }

    [Fact]
    public async Task Refresh_OldResultsStayUntilSwap_AndEventsRaisedOnce()
    {
        await RunAsync("SELECT * FROM t");
        var old = _tab.Results[0];
        var replacing = 0;
        var replaced = 0;
        _tab.ResultsReplacing += () => replacing++;
        _tab.ResultsReplaced += () => replaced++;
        var gate = new TaskCompletionSource();
        _gated.Gate = (_, _) => gate.Task;

        var refresh = _tab.RefreshAsync(CancellationToken.None);

        Assert.True(_tab.IsRefreshing);
        Assert.True(_tab.IsRunning);
        Assert.Same(old, Assert.Single(_tab.Results));
        Assert.Equal(0, replacing);
        gate.SetResult();
        Assert.Equal(RefreshOutcome.Refreshed, await refresh);

        Assert.NotSame(old, Assert.Single(_tab.Results));
        Assert.Equal((1, 1), (replacing, replaced));
        Assert.False(_tab.IsRefreshing);
        Assert.False(_tab.IsRunning);
    }

    [Fact]
    public async Task Refresh_KeepsSelectedResultFiltersSearchSort()
    {
        _editor.Text = "SELECT * FROM t; SELECT id FROM t";
        await _tab.RunAllCommand.ExecuteAsync(null);
        _tab.SelectedResult = _tab.Results[0];
        var view = _tab.Results[0].View!;
        view.AddFilter(new GridFilter(1, GridFilterOperator.Equals, "a", null));
        view.SearchText = "a";
        view.SetSort(new GridSort(1, false));

        Assert.Equal(RefreshOutcome.Refreshed, await _tab.RefreshAsync(CancellationToken.None));

        Assert.Same(_tab.Results[0], _tab.SelectedResult);
        var kept = _tab.Results[0].View!;
        Assert.Single(kept.Filters);
        Assert.Equal("a", kept.SearchText);
        Assert.Equal(new GridSort(1, false), kept.Sort);
        Assert.Equal(1, _tab.Results[0].DisplayGrid!.RowCount);
    }

    [Fact]
    public async Task Refresh_KeepsSelectionClamped()
    {
        await RunAsync("SELECT id, name FROM t");
        var selection = _tab.Results[0].Selection!;
        selection.SelectCell(2, 1, false);
        selection.SelectCell(4, 1, true);
        await Exec("DELETE FROM t WHERE id > 4");

        await _tab.RefreshAsync(CancellationToken.None);

        var kept = _tab.Results[0].Selection!;
        Assert.True(kept.HasSelection);
        Assert.Equal(3, kept.ActiveRow);
        Assert.Equal(2, kept.FirstRow(4));
    }

    [Fact]
    public async Task Refresh_AnchorPastEnd_ClearsSelection()
    {
        await RunAsync("SELECT id, name FROM t");
        _tab.Results[0].Selection!.SelectCell(8, 1, false);
        await Exec("DELETE FROM t WHERE id > 5");

        await _tab.RefreshAsync(CancellationToken.None);

        Assert.False(_tab.Results[0].Selection!.HasSelection);
    }

    [Fact]
    public async Task Refresh_ColumnsChanged_DoesNotKeepView()
    {
        await RunAsync("SELECT * FROM t");
        _tab.Results[0].View!.AddFilter(new GridFilter(1, GridFilterOperator.Equals, "a", null));
        await Exec("ALTER TABLE t ADD COLUMN extra TEXT");

        await _tab.RefreshAsync(CancellationToken.None);

        Assert.Empty(_tab.Results[0].View!.Filters);
        Assert.Equal(10, _tab.Results[0].DisplayGrid!.RowCount);
    }

    [Fact]
    public async Task Refresh_Cancelled_KeepsOldResults()
    {
        await RunAsync("SELECT * FROM t");
        var old = _tab.Results[0];
        using var cts = new CancellationTokenSource();
        _gated.Gate = (_, ct) =>
        {
            cts.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        };

        Assert.Equal(RefreshOutcome.Cancelled, await _tab.RefreshAsync(cts.Token));

        Assert.Same(old, Assert.Single(_tab.Results));
        Assert.False(_tab.IsRunning);
        Assert.False(_tab.IsRefreshing);
    }

    [Fact]
    public async Task Refresh_StatementError_SwapsAndReturnsFailed()
    {
        await RunAsync("SELECT * FROM t");
        await Exec("DROP TABLE t");

        Assert.Equal(RefreshOutcome.Failed, await _tab.RefreshAsync(CancellationToken.None));

        Assert.Equal(ResultKind.Message, Assert.Single(_tab.Results).Kind);
    }

    [Fact]
    public async Task Refresh_Skipped_WhenNeverRun()
    {
        Assert.Equal(RefreshOutcome.Skipped, await _tab.RefreshAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Refresh_Skipped_WhenPendingChanges()
    {
        await RunAsync("SELECT * FROM t");
        Assert.Null(_tab.Results[0].Editor!.TrySetCell(0, 1, "changed"));
        await AssertSkippedAsync();
    }

    [Fact]
    public async Task Refresh_Skipped_WhenEditing()
    {
        await RunAsync("SELECT * FROM t");
        _tab.Results[0].Selection!.SelectCell(0, 1, false);
        Assert.True(_tab.Results[0].EditController!.Begin());
        await AssertSkippedAsync();
    }

    [Fact]
    public async Task Refresh_Skipped_WhenRunning()
    {
        await RunAsync("SELECT * FROM t");
        var gate = new TaskCompletionSource();
        _gated.Gate = (_, _) => gate.Task;
        var run = _tab.RunCommand.ExecuteAsync(null);
        Assert.True(_tab.IsRunning);

        Assert.Equal(RefreshOutcome.Skipped, await _tab.RefreshAsync(CancellationToken.None));

        gate.SetResult();
        await run;
    }

    private async Task AssertSkippedAsync()
    {
        var results = _tab.Results.ToList();
        _gated.Executed.Clear();

        Assert.Equal(RefreshOutcome.Skipped, await _tab.RefreshAsync(CancellationToken.None));

        Assert.Equal(results, _tab.Results);
        Assert.Empty(_gated.Executed);
    }

    private sealed class GatedConnection : IDatabaseConnection
    {
        public IDatabaseConnection Inner { get; set; } = null!;
        public Func<string, CancellationToken, Task>? Gate { get; set; }
        public List<string> Executed { get; } = [];
        public List<IReadOnlyList<QueryParameter>> Bound { get; } = [];
        public string ServerVersion => Inner.ServerVersion;

        public async ValueTask<IResultReader> ExecuteAsync(string sql, IReadOnlyList<QueryParameter> parameters, CancellationToken cancellationToken)
        {
            Executed.Add(sql);
            Bound.Add(parameters);
            if (Gate is not null)
                await Gate(sql, cancellationToken);
            return await Inner.ExecuteAsync(sql, parameters, cancellationToken);
        }

        public ValueTask<IReadOnlyList<string>> ListDatabasesAsync(CancellationToken cancellationToken) => Inner.ListDatabasesAsync(cancellationToken);

        public ValueTask<SchemaSnapshot> ReadSchemaAsync(CancellationToken cancellationToken) => Inner.ReadSchemaAsync(cancellationToken);

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

        public ValueTask<IReadOnlyList<HistoryEntry>> ListAsync(int max, CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<HistoryEntry>>(Entries);

        public ValueTask ClearAsync(CancellationToken ct) => ValueTask.CompletedTask;
    }

    private sealed class Settings : ISettingsStore
    {
        public ValueTask<AppSettings> LoadAsync(CancellationToken ct) => ValueTask.FromResult(AppSettings.Default);

        public ValueTask SaveAsync(AppSettings settings, CancellationToken ct) => ValueTask.CompletedTask;
    }
}
