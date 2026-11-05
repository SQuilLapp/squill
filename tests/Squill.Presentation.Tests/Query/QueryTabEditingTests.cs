using Squill.Core;
using Squill.Data;
using Squill.Data.Sqlite;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation.Editor;
using Squill.Presentation.Explorer;
using Squill.Presentation.Query;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.Presentation.Tests.Query;

public sealed class QueryTabEditingTests : IAsyncLifetime
{
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
            "INSERT INTO t (name) VALUES ('a'), ('b'), ('c')",
            "CREATE TABLE nopk (x INTEGER, y TEXT)",
            "INSERT INTO nopk VALUES (1, 'x')",
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

    [Fact]
    public async Task SingleTableWithPrimaryKey_IsEditable()
    {
        var result = await RunAsync("SELECT * FROM t");

        Assert.NotNull(result.Editor);
        Assert.NotNull(result.EditController);
        Assert.NotNull(result.Selection);
        Assert.Null(result.ReadOnlyReason);
        Assert.Same(result.Editor.Grid, result.DisplayGrid);
        Assert.NotSame(result.Grid, result.DisplayGrid);
    }

    [Fact]
    public async Task CountText_IsTheLoadedRowCount()
    {
        var result = await RunAsync("SELECT * FROM t");
        Assert.Equal($"{result.Grid!.RowCount:N0}", result.CountText);
    }

    [Theory]
    [InlineData("SELECT t.id FROM t JOIN nopk ON nopk.x = t.id")]
    [InlineData("SELECT * FROM nopk")]
    [InlineData("SELECT count(*) FROM t")]
    [InlineData("SELECT * FROM v")]
    public async Task OtherShapes_AreReadOnlyWithReason(string sql)
    {
        var result = await RunAsync(sql);

        Assert.Null(result.Editor);
        Assert.Null(result.EditController);
        Assert.False(string.IsNullOrEmpty(result.ReadOnlyReason));
        Assert.Same(result.Grid, result.DisplayGrid);
    }

    [Fact]
    public async Task MessageResult_HasNoEditorAndNoReason()
    {
        var result = await RunAsync("UPDATE t SET name = name WHERE 1 = 0");

        Assert.Equal(ResultKind.Message, result.Kind);
        Assert.Null(result.Editor);
        Assert.Null(result.ReadOnlyReason);
    }

    [Fact]
    public async Task SelectTopFromExplorer_IsEditable()
    {
        var table = _tab.Session.Schema.FindTable(null, "t")!;

        var result = await RunAsync(SqlSnippets.SelectTop(table, DatabaseDialect.Sqlite));

        Assert.NotNull(result.Editor);
    }

    [Fact]
    public async Task Header_ShowsMarkerWhilePending()
    {
        var result = await RunAsync("SELECT * FROM t");
        Assert.DoesNotContain('*', result.Header);

        result.Editor!.TrySetCell(0, 1, "z");
        Assert.EndsWith(" *", result.Header);

        result.Editor.DiscardCommand.Execute(null);
        Assert.DoesNotContain('*', result.Header);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Export_WithPendingChanges_AsksThenWritesTheCommittedValues(bool accept)
    {
        var result = await RunAsync("SELECT * FROM t");
        result.Editor!.TrySetCell(0, 1, "edited");
        _dialogs.ConfirmResult = accept;
        _dialogs.SavePathResult = Path.Combine(Path.GetTempPath(), $"squill-{Guid.NewGuid():N}.csv");
        try
        {
            await result.ExportCommand.ExecuteAsync(new Squill.Presentation.Results.CsvResultExporter());

            Assert.Single(_dialogs.Confirms);
            Assert.Equal(accept, File.Exists(_dialogs.SavePathResult));
            if (accept)
            {
                var csv = await File.ReadAllTextAsync(_dialogs.SavePathResult, TestContext.Current.CancellationToken);
                Assert.Contains("1,a\r\n", csv);
                Assert.DoesNotContain("edited", csv);
            }
        }
        finally
        {
            File.Delete(_dialogs.SavePathResult);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Run_WithPendingChanges_AsksFirst(bool accept)
    {
        var result = await RunAsync("SELECT * FROM t");
        result.Editor!.TrySetCell(0, 1, "z");
        _dialogs.ConfirmResult = accept;

        await _tab.RunCommand.ExecuteAsync(null);

        Assert.Equal(("Exécuter la requête", "Abandonner les modifications en attente de la grille ?"), Assert.Single(_dialogs.Confirms));
        Assert.Equal(accept, !ReferenceEquals(result, _tab.Results[0]));
    }

    [Fact]
    public async Task EndToEnd_EditAddDeleteCommit_PersistsAndReloads()
    {
        var result = await RunAsync("SELECT * FROM t ORDER BY id");
        var editor = result.Editor!;
        Assert.Null(editor.TrySetCell(0, 1, "A2"));
        var added = editor.AddRow();
        Assert.Null(editor.TrySetCell(added, 1, "d"));
        editor.DeleteRows([1]);

        await editor.CommitCommand.ExecuteAsync(null);
        Assert.False(editor.HasPendingChanges);

        var reloaded = await RunAsync("SELECT * FROM t ORDER BY id");
        var grid = reloaded.Grid!;
        Assert.Equal(3, grid.RowCount);
        Assert.Equal(["A2", "c", "d"], Enumerable.Range(0, 3).Select(i => (string?)grid.GetCell(i, 1)));
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
