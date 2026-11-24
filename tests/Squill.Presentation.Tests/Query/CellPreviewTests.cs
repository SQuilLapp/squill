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
            "INSERT INTO t (short, doc) VALUES ('one' || char(13) || char(10) || 'two', NULL)",
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

    // Queues what the view-model resumes on the "UI thread" until the test pumps it, so the in-between state is observable.
    private sealed class QueueContext : SynchronizationContext, IDisposable
    {
        private readonly SynchronizationContext? _previous = Current;
        private readonly System.Collections.Concurrent.BlockingCollection<Action> _queue = [];

        public QueueContext() => SetSynchronizationContext(this);

        public override void Post(SendOrPostCallback d, object? state) => _queue.Add(() => d(state));

        public void Pump(Task until)
        {
            while (!until.IsCompleted)
            {
                if (_queue.TryTake(out var work, 20))
                    work();
            }
        }

        public void Dispose() => SetSynchronizationContext(_previous);
    }

    [Fact]
    public async Task Refresh_ValidJson_ShowsIndentedTextWithoutError()
    {
        var result = await RunAsync("SELECT * FROM t ORDER BY id");

        result.Selection!.SelectCell(0, 2, false);

        Assert.Null(result.Preview!.Error);
        Assert.Contains("\n", result.Preview.Text);
        Assert.False(result.Preview.IsDirty);
    }

    [Fact]
    public async Task Text_LongJson_ValidatesInBackground()
    {
        var result = await RunAsync("SELECT * FROM t ORDER BY id");
        result.Selection!.SelectCell(0, 2, false);
        var preview = result.Preview!;
        using var ui = new QueueContext();

        preview.Text = "{\"a\":\"" + new string('x', 150_000);

        Assert.True(preview.IsWorking);
        Assert.False(preview.ApplyCommand.CanExecute(null));
        ui.Pump(preview.Pending);
        Assert.False(preview.IsWorking);
        Assert.False(string.IsNullOrEmpty(preview.Error));
    }

    [Fact]
    public async Task Text_LongJson_LatestRequestWins()
    {
        var result = await RunAsync("SELECT * FROM t ORDER BY id");
        result.Selection!.SelectCell(0, 2, false);
        var preview = result.Preview!;
        using var ui = new QueueContext();

        preview.Text = "{\"a\":\"" + new string('x', 150_000);
        preview.Text = "{\"a\":\"" + new string('x', 150_000) + "\"}";
        ui.Pump(preview.Pending);

        Assert.False(preview.IsWorking);
        Assert.Null(preview.Error);
        Assert.True(preview.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public async Task Refresh_LongJson_SetsTextOnce()
    {
        var result = await RunAsync("SELECT '{\"k\":\"' || replace(hex(zeroblob(60000)), '0', 'a') || '\"}' AS doc");
        var preview = result.Preview!;
        using var ui = new QueueContext();
        var changes = 0;
        preview.PropertyChanged += (_, e) => changes += e.PropertyName == nameof(preview.Text) ? 1 : 0;

        result.Selection!.SelectCell(0, 0, false);

        Assert.True(preview.IsWorking);
        Assert.Equal(0, changes);
        ui.Pump(preview.Pending);
        Assert.False(preview.IsWorking);
        Assert.Equal(1, changes);
        Assert.True(preview.IsJson);
        Assert.Contains("\n", preview.Text);
    }

    [Fact]
    public async Task Refresh_JsonValue_TextHasNoCr()
    {
        var result = await RunAsync("SELECT * FROM t ORDER BY id");

        result.Selection!.SelectCell(0, 2, false);

        Assert.DoesNotContain('\r', result.Preview!.Text);
    }

    [Fact]
    public async Task Text_DiffersOnlyByLineBreaks_IsNotDirty()
    {
        var result = await RunAsync("SELECT * FROM t ORDER BY id");
        result.Selection!.SelectCell(0, 2, false);
        var preview = result.Preview!;

        preview.Text = preview.Text.Replace("\n", "\r");

        Assert.False(preview.IsDirty);
        Assert.False(preview.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public async Task Text_TextValueWithCrLf_RoundTripIsNotDirty()
    {
        var result = await RunAsync("SELECT * FROM t ORDER BY id");
        result.Selection!.SelectCell(4, 1, false);
        var preview = result.Preview!;
        Assert.True(preview.IsOpen);

        preview.Text = "one\rtwo";

        Assert.False(preview.IsDirty);
    }

    [Fact]
    public async Task Apply_CrLineBreaks_StoresLf()
    {
        var result = await RunAsync("SELECT * FROM t ORDER BY id");
        result.Selection!.SelectCell(0, 2, false);
        var preview = result.Preview!;

        preview.Text = "{\r  \"a\": 2\r}";
        preview.ApplyCommand.Execute(null);

        Assert.Equal("{\n  \"a\": 2\n}", result.Editor!.Grid.GetCell(0, 2));
        Assert.False(preview.IsDirty);
    }

    [Fact]
    public async Task Apply_MixedLineBreaks_StoresLf()
    {
        var result = await RunAsync("SELECT * FROM t ORDER BY id");
        result.Selection!.SelectCell(2, 2, false);

        result.Preview!.Text = "a\r\nb\nc\rd";
        result.Preview.ApplyCommand.Execute(null);

        Assert.Equal("a\nb\nc\nd", result.Editor!.Grid.GetCell(2, 2));
    }

    [Fact]
    public async Task Apply_300KbIndentedJsonWithCr_StoresLfText()
    {
        var result = await RunAsync("SELECT * FROM t ORDER BY id");
        result.Selection!.SelectCell(0, 2, false);
        var preview = result.Preview!;
        using var ui = new QueueContext();
        var lf = "{\n" + string.Concat(Enumerable.Range(0, 10_000).Select(i => $"  \"k{i}\": \"vvvvvvvvvvvvvvvvvvvv\",\n")) + "  \"z\": 1\n}";
        var cr = lf.Replace("\n", "\r");

        preview.Text = cr;
        ui.Pump(preview.Pending);
        preview.ApplyCommand.Execute(null);
        ui.Pump(preview.Pending);

        var stored = (string)result.Editor!.Grid.GetCell(0, 2)!;
        Assert.Equal(lf, stored);
        Assert.Equal(cr.Length, stored.Length);
    }

    [Fact]
    public async Task Apply_EditedMinifiedJson_StoresShownIndentedText()
    {
        var result = await RunAsync("SELECT * FROM t ORDER BY id");
        result.Selection!.SelectCell(0, 2, false);
        var preview = result.Preview!;

        preview.Text = preview.Text.Replace("\"a\": 1", "\"a\": 2");
        preview.ApplyCommand.Execute(null);

        var stored = (string)result.Editor!.Grid.GetCell(0, 2)!;
        Assert.Contains("\"a\": 2", stored);
        Assert.Contains('\n', stored);
        Assert.DoesNotContain('\r', stored);
    }

    [Fact]
    public async Task Text_ShortJson_StaysSynchronous()
    {
        var result = await RunAsync("SELECT * FROM t ORDER BY id");
        result.Selection!.SelectCell(0, 2, false);
        var preview = result.Preview!;
        var working = false;
        preview.PropertyChanged += (_, e) => working |= e.PropertyName == nameof(preview.IsWorking) && preview.IsWorking;

        preview.Text = "{\"a\": 2}";

        Assert.False(working);
        Assert.Null(preview.Error);
    }
}
