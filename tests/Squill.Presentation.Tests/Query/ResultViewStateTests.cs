using Squill.Core;
using Squill.Data;
using Squill.Data.Sqlite;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation.Editor;
using Squill.Presentation.Localization;
using Squill.Presentation.Query;
using Squill.Presentation.Results;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.Presentation.Tests.Query;

public sealed class ResultViewStateTests : IAsyncLifetime
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"squill-{Guid.NewGuid():N}.db");
    private readonly FakeDialogService _dialogs = new();
    private IDatabaseConnection _connection = null!;
    private StubSqlEditor _editor = null!;
    private QueryTabViewModel _tab = null!;

    public async ValueTask InitializeAsync()
    {
        var profile = new ConnectionProfile(Guid.NewGuid(), "p", DatabaseDialect.Sqlite, null, null, null, null, _file, null);
        _connection = await new SqliteDatabaseProvider().OpenAsync(profile, null, CancellationToken.None);
        foreach (var sql in new[]
        {
            "CREATE TABLE t (id INTEGER PRIMARY KEY, name TEXT NOT NULL)",
            "INSERT INTO t (name) VALUES ('alpha'), ('beta'), ('gamma'), ('alphabet')",
            "CREATE VIEW v AS SELECT id, name FROM t",
        })
            await (await _connection.ExecuteAsync(sql, [], CancellationToken.None)).DisposeAsync();
        var schema = await _connection.ReadSchemaAsync(CancellationToken.None);
        _editor = new StubSqlEditor();
        _tab = new QueryTabViewModel(new ConnectionSession(profile, _connection, schema, null), _editor, new History(), new Settings(), new ImmediateDispatcher(), _dialogs);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        try
        {
            File.Delete(_file);
        }
        catch (IOException)
        {
        }
    }

    private async Task<ResultViewModel> RunAsync(string sql)
    {
        _editor.Text = sql;
        await _tab.RunCommand.ExecuteAsync(null);
        return _tab.Results[0];
    }

    private static GridFilter NameContains(IGridDataSource grid, string text)
    {
        Assert.True(GridFilterMatcher.TryCreate(grid.Columns[1], GridFilterOperator.Contains, text, out var filter, out _));
        return filter;
    }

    private static async Task<string> NamesAsync(ResultViewModel result)
    {
        if (result.DisplayGrid is FilteredGridDataSource view)
            await view.WhenIdle().WaitAsync(Bound, TestContext.Current.CancellationToken);
        var grid = result.DisplayGrid!;
        return string.Join(",", Enumerable.Range(0, (int)grid.RowCount).Select(i => grid.GetCell(i, 1)));
    }

    [Fact]
    public async Task AddFilter_SwapsDisplayGridAndRaisesTheChange()
    {
        var result = await RunAsync("SELECT * FROM t");
        var raised = 0;
        result.PropertyChanged += (_, e) => raised += e.PropertyName == nameof(ResultViewModel.DisplayGrid) ? 1 : 0;

        result.View!.AddFilter(NameContains(result.DisplayGrid!, "alpha"));

        Assert.IsType<FilteredGridDataSource>(result.DisplayGrid);
        Assert.True(raised > 0);
        Assert.Equal("alpha,alphabet", await NamesAsync(result));
        Assert.Equal(GridFilterText.Format(result.View.Filters[0].Filter, result.Grid!.Columns[1]), result.View.Filters[0].Text);
    }

    [Fact]
    public async Task ClearFilters_RestoresTheSameGridInstance()
    {
        var result = await RunAsync("SELECT * FROM t");
        var before = result.DisplayGrid;
        result.View!.AddFilter(NameContains(before!, "beta"));
        result.View.SetSort(new GridSort(1, true));

        result.View.ClearFilters();

        Assert.Same(before, result.DisplayGrid);
        Assert.False(result.View.IsActive);
        Assert.Empty(result.View.Filters);
        Assert.Null(result.View.Sort);
        Assert.Equal("", result.View.SearchText);
    }

    [Fact]
    public async Task SearchText_FiltersAnyColumn()
    {
        var result = await RunAsync("SELECT * FROM t");

        result.View!.SearchText = "GAM";

        Assert.True(result.View.IsActive);
        Assert.Equal("gamma", await NamesAsync(result));
        result.View.SearchText = "";
        Assert.False(result.View.IsActive);
    }

    [Fact]
    public async Task ToggleSort_CyclesAscendingDescendingNone()
    {
        var result = await RunAsync("SELECT * FROM t");

        result.View!.ToggleSort(1);
        Assert.Equal("alpha,alphabet,beta,gamma", await NamesAsync(result));
        result.View.ToggleSort(1);
        Assert.Equal("gamma,beta,alphabet,alpha", await NamesAsync(result));
        result.View.ToggleSort(1);
        Assert.Null(result.View.Sort);
        Assert.False(result.View.IsActive);
        Assert.Equal("alpha,beta,gamma,alphabet", await NamesAsync(result));
    }

    [Theory]
    [InlineData(UiLanguage.French, "2 lignes sur 4 · filtre actif", "1 ligne sur 4 · filtre actif")]
    [InlineData(UiLanguage.English, "2 rows of 4 · filter active", "1 row of 4 · filter active")]
    public async Task FooterText_UnfilteredAndFiltered(UiLanguage language, string two, string one)
    {
        using var _ = UiText.Override(language);
        var result = await RunAsync("SELECT * FROM t");
        Assert.Equal(language == UiLanguage.French ? "Lignes 1–4 sur 4 chargées" : "Rows 1–4 of 4 loaded", result.View!.FooterText);

        result.View.AddFilter(NameContains(result.DisplayGrid!, "alpha"));
        await NamesAsync(result);
        Assert.Equal(two, result.View.FooterText);

        result.View.SearchText = "bet";
        await NamesAsync(result);
        Assert.Equal(one, result.View.FooterText);
    }

    [Fact]
    public async Task EveryChange_ClearsTheSelection()
    {
        var result = await RunAsync("SELECT * FROM t");
        result.Selection!.SelectCell(2, 1, false);

        result.View!.SearchText = "a";
        Assert.False(result.Selection.HasSelection);

        result.Selection.SelectCell(1, 1, false);
        result.View.ClearFilters();
        Assert.False(result.Selection.HasSelection);
    }

    [Fact]
    public async Task EditController_RefusesWhileActive_AndWorksAfterClear()
    {
        var result = await RunAsync("SELECT * FROM t");
        var controller = result.EditController!;
        var editor = result.Editor!;
        result.Selection!.SelectCell(0, 1, false);
        Assert.True(controller.Begin());
        controller.Cancel();

        result.View!.SearchText = "a";
        result.Selection.SelectCell(0, 1, false);

        Assert.False(controller.Begin());
        Assert.NotNull(editor.TrySetCell(0, 1, "x"));
        Assert.NotNull(editor.TrySetDefault(0, 1));
        Assert.Equal(-1, controller.AddRow());
        controller.DeleteSelectedRows();
        controller.RevertSelectedRows();
        controller.ClearSelection();
        editor.SetNull(0, 1);
        Assert.False(editor.HasPendingChanges);
        Assert.Equal(4, editor.Grid.RowCount);

        result.View.ClearFilters();
        result.Selection.SelectCell(0, 1, false);

        Assert.True(controller.Begin());
        controller.Cancel();
        Assert.Null(editor.TrySetCell(0, 1, "x"));
        Assert.True(editor.HasPendingChanges);
    }

    [Fact]
    public async Task ReadOnlyReason_RestoresItsPreviousValue()
    {
        var editable = await RunAsync("SELECT * FROM t");
        Assert.Null(editable.ReadOnlyReason);
        editable.View!.SearchText = "a";
        Assert.Equal(UiText.Get("results.readOnly.filtered"), editable.ReadOnlyReason);
        editable.View.ClearFilters();
        Assert.Null(editable.ReadOnlyReason);

        var readOnly = await RunAsync("SELECT * FROM v");
        var reason = readOnly.ReadOnlyReason;
        Assert.False(string.IsNullOrEmpty(reason));
        readOnly.View!.SearchText = "a";
        Assert.NotEqual(reason, readOnly.ReadOnlyReason);
        readOnly.View.ClearFilters();
        Assert.Equal(reason, readOnly.ReadOnlyReason);
    }

    [Fact]
    public void ReadOnlyReason_RestoresLoading()
    {
        var grid = new StreamingGridDataSource(new NoRowsReader(), 10, new ImmediateDispatcher());
        var result = ResultViewModel.ForGrid("r", grid, _dialogs, new ImmediateDispatcher());
        Assert.Equal("loading", result.ReadOnlyReason);

        result.View!.SearchText = "x";
        Assert.NotEqual("loading", result.ReadOnlyReason);
        result.View.ClearFilters();

        Assert.Equal("loading", result.ReadOnlyReason);
        Assert.Same(grid, result.DisplayGrid);
    }

    [Fact]
    public async Task PendingChange_BlocksFiltersWithTheReason()
    {
        var result = await RunAsync("SELECT * FROM t");
        Assert.True(result.View!.CanChange);
        Assert.Null(result.View.BlockedReason);
        result.Editor!.TrySetCell(0, 1, "edited");

        Assert.False(result.View.CanChange);
        Assert.Equal(UiText.Get("results.filter.blocked"), result.View.BlockedReason);
        result.View.AddFilter(NameContains(result.DisplayGrid!, "alpha"));
        result.View.ToggleSort(1);
        result.View.SearchText = "a";
        Assert.False(result.View.IsActive);
        Assert.Same(result.Editor.Grid, result.DisplayGrid);

        result.Editor.DiscardCommand.Execute(null);
        Assert.True(result.View.CanChange);
        Assert.Null(result.View.BlockedReason);
    }

    [Fact]
    public async Task EditorSetLater_KeepsAnActiveView()
    {
        var grid = new StreamingGridDataSource(new NoRowsReader(), 10, new ImmediateDispatcher());
        var result = ResultViewModel.ForGrid("r", grid, _dialogs, new ImmediateDispatcher());
        result.View!.SearchText = "x";
        var before = Assert.IsType<FilteredGridDataSource>(result.DisplayGrid);

        var run = await RunAsync("SELECT * FROM t");
        var editable = run.Editor!;
        result.SetEditor(editable);

        var after = Assert.IsType<FilteredGridDataSource>(result.DisplayGrid);
        Assert.NotSame(before, after);
        Assert.True(editable.Grid.IsViewActive);
        result.View.ClearFilters();
        Assert.Same(editable.Grid, result.DisplayGrid);
        Assert.False(editable.Grid.IsViewActive);
    }

    [Fact]
    public async Task Export_StillWritesEveryRowWhileAFilterIsActive()
    {
        var result = await RunAsync("SELECT * FROM t");
        result.View!.AddFilter(NameContains(result.DisplayGrid!, "beta"));
        _dialogs.SavePathResult = Path.Combine(Path.GetTempPath(), $"squill-{Guid.NewGuid():N}.csv");
        try
        {
            await result.ExportCommand.ExecuteAsync(new CsvResultExporter());

            Assert.Equal(5, (await File.ReadAllLinesAsync(_dialogs.SavePathResult, TestContext.Current.CancellationToken)).Length);
        }
        finally
        {
            File.Delete(_dialogs.SavePathResult);
        }
    }

    private sealed class NoRowsReader : IResultReader
    {
        public IReadOnlyList<ColumnInfo> Columns { get; } = [new("id", "int", typeof(int), true, 0), new("name", "text", typeof(string), true, 1)];

        public long RecordsAffected => -1;

        public ValueTask<object?[][]?> ReadBatchAsync(int maxRows, CancellationToken cancellationToken) => ValueTask.FromResult<object?[][]?>(null);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class History : IQueryHistoryStore
    {
        public ValueTask AppendAsync(HistoryEntry e, CancellationToken ct) => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<HistoryEntry>> ListAsync(int max, CancellationToken ct) => ValueTask.FromResult<IReadOnlyList<HistoryEntry>>([]);

        public ValueTask ClearAsync(CancellationToken ct) => ValueTask.CompletedTask;
    }

    private sealed class Settings : ISettingsStore
    {
        public ValueTask<AppSettings> LoadAsync(CancellationToken ct) => ValueTask.FromResult(AppSettings.Default);

        public ValueTask SaveAsync(AppSettings settings, CancellationToken ct) => ValueTask.CompletedTask;
    }
}
