using Squill.Core;
using Squill.Data;
using Squill.Data.Sqlite;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation.Editor;
using Squill.Presentation.Query;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.Presentation.Tests.Query;

public sealed class CellPreviewTests : IAsyncLifetime
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"squill-{Guid.NewGuid():N}.db");
    private IDatabaseConnection _connection = null!;
    private StubSqlEditor _editor = null!;
    private QueryTabViewModel _tab = null!;

    public async ValueTask InitializeAsync()
    {
        var profile = new ConnectionProfile(Guid.NewGuid(), "p", DatabaseDialect.Sqlite, null, null, null, null, _file, null);
        _connection = await new SqliteDatabaseProvider().OpenAsync(profile, null, CancellationToken.None);
        foreach (var sql in new[]
        {
            "CREATE TABLE t (id INTEGER PRIMARY KEY, short TEXT, doc JSON)",
            "INSERT INTO t (short, doc) VALUES ('abc', '{\"a\":1,\"b\":[1,2]}'), ('x', NULL), ('y', 'line one' || char(10) || 'line two')",
            "INSERT INTO t (short, doc) VALUES ('z', '" + new string('q', 120) + "')",
        })
            await (await _connection.ExecuteAsync(sql, [], CancellationToken.None)).DisposeAsync();
        var schema = await _connection.ReadSchemaAsync(CancellationToken.None);
        _editor = new StubSqlEditor();
        _tab = new QueryTabViewModel(new ConnectionSession(profile, _connection, schema, null), _editor, new History(), new Settings(), new ImmediateDispatcher(), new FakeDialogService());
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
    public async Task SelectingAShortValue_KeepsThePaneClosed()
    {
        var result = await RunAsync("SELECT * FROM t ORDER BY id");

        result.Selection!.SelectCell(0, 1, false);

        Assert.False(result.Preview!.IsOpen);
    }

    [Fact]
    public async Task SelectingAJsonValue_OpensThePaneWithIndentedText_AndEditsItThroughTheCell()
    {
        var result = await RunAsync("SELECT * FROM t ORDER BY id");

        result.Selection!.SelectCell(0, 2, false);
        var preview = result.Preview!;

        Assert.True(preview.IsOpen);
        Assert.True(preview.IsJson);
        Assert.Equal("doc", preview.Title);
        Assert.Contains("\n", preview.Text);
        Assert.True(preview.CanEdit);
        Assert.False(preview.IsDirty);

        preview.Text = "{\"a\": 2}";
        Assert.True(preview.IsDirty);
        preview.ApplyCommand.Execute(null);

        Assert.Equal("{\"a\": 2}", result.Editor!.Grid.GetCell(0, 2));
        Assert.True(result.Editor.HasPendingChanges);
    }

    [Fact]
    public async Task InvalidJsonDraft_ShowsAnErrorAndCannotBeApplied()
    {
        var result = await RunAsync("SELECT * FROM t ORDER BY id");
        result.Selection!.SelectCell(0, 2, false);
        var preview = result.Preview!;

        preview.Text = "{\"a\": ";

        Assert.False(string.IsNullOrEmpty(preview.Error));
        Assert.False(preview.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public async Task LongOrMultiLineText_OpensThePane_NullDoesNot_AndCloseHidesItUntilAnotherCell()
    {
        var result = await RunAsync("SELECT * FROM t ORDER BY id");
        var preview = result.Preview!;

        result.Selection!.SelectCell(1, 2, false);
        Assert.False(preview.IsOpen);

        result.Selection.SelectCell(2, 2, false);
        Assert.True(preview.IsOpen);
        Assert.False(preview.IsJson);

        preview.CloseCommand.Execute(null);
        Assert.False(preview.IsOpen);

        result.Selection.SelectCell(3, 2, false);
        Assert.True(preview.IsOpen);
        Assert.Equal(120, preview.Text.Length);
    }

    [Fact]
    public async Task ReadOnlyResult_PreviewsButCannotEdit()
    {
        var result = await RunAsync("SELECT doc || '' AS d FROM t ORDER BY id");

        result.Selection!.SelectCell(0, 0, false);

        Assert.True(result.Preview!.IsOpen);
        Assert.False(result.Preview.CanEdit);
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
