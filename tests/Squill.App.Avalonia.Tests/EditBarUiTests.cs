using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using Squill.App.Avalonia.Controls;
using Squill.App.Avalonia.Editors;
using Squill.App.Avalonia.Views;
using Squill.Core;
using Squill.Data;
using Squill.Data.Sqlite;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation;
using Squill.Presentation.Query;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class EditBarUiTests
{
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

    private sealed class RecordingDialogs(List<string> extensions) : IDialogService
    {
        public ValueTask<Squill.Presentation.Safety.ProductionConfirmationResult> ConfirmProductionAsync(Squill.Presentation.Safety.ProductionConfirmationRequest request) =>
            ValueTask.FromResult(new Squill.Presentation.Safety.ProductionConfirmationResult(true, false));

        public ValueTask<bool> ConfirmAsync(string title, string message) => ValueTask.FromResult(true);
        public ValueTask<string?> PromptAsync(string title, string message, string? initial) => ValueTask.FromResult<string?>(null);

        public ValueTask<string?> PickSaveFileAsync(string suggestedName, string extension)
        {
            extensions.Add(extension);
            return ValueTask.FromResult<string?>(null);
        }

        public ValueTask ShowErrorAsync(string title, string message) => ValueTask.CompletedTask;
        public ValueTask<IReadOnlyList<ParameterValue>?> PromptParametersAsync(
            IReadOnlyList<string> names,
            IReadOnlyDictionary<string, ParameterValue> defaults) => ValueTask.FromResult<IReadOnlyList<ParameterValue>?>(null);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _file = Path.Combine(Path.GetTempPath(), $"squill-bar-{Guid.NewGuid():N}.db");
        private IDatabaseConnection _connection = null!;

        public QueryTabViewModel Tab { get; private set; } = null!;
        public QueryTabView View { get; } = new();
        public Window Window { get; private set; } = null!;
        public List<string> Scripts { get; } = [];
        public List<string> Extensions { get; } = [];

        public static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture();
            var profile = new ConnectionProfile(Guid.NewGuid(), "p", DatabaseDialect.Sqlite, null, null, null, null, f._file, null);
            f._connection = await new SqliteDatabaseProvider().OpenAsync(profile, null, CancellationToken.None);
            foreach (var sql in new[]
            {
                "CREATE TABLE t (id INTEGER PRIMARY KEY, name TEXT NOT NULL)",
                "INSERT INTO t (name) VALUES ('a'), ('b')",
                "CREATE TABLE nopk (x INTEGER)",
            })
                await (await f._connection.ExecuteAsync(sql, [], CancellationToken.None)).DisposeAsync();
            var schema = await f._connection.ReadSchemaAsync(CancellationToken.None);
            f.Tab = new QueryTabViewModel(new ConnectionSession(profile, f._connection, schema, null), new AvaloniaSqlEditor(), new History(), new Settings(), new ImmediateDispatcher(), new RecordingDialogs(f.Extensions));
            f.Tab.ScriptRequested += f.Scripts.Add;
            f.View.DataContext = f.Tab;
            f.Window = new Window { Width = 900, Height = 500, Content = f.View };
            f.Window.Show();
            return f;
        }

        public async Task RunAsync(string sql)
        {
            Tab.Editor.Text = sql;
            await Tab.RunCommand.ExecuteAsync(null);
        }

        public T Find<T>(string name)
            where T : Control => View.FindControl<T>(name)!;

        public async ValueTask DisposeAsync()
        {
            Window.Close();
            await _connection.DisposeAsync();
            try
            {
                File.Delete(_file);
            }
            catch (IOException)
            {
            }
        }
    }

    private static async Task RunAsync(Func<Task> body) => await Headless.Session.Dispatch(body, TestContext.Current.CancellationToken);

    [Fact]
    public Task EditableResult_ShowsTheMarkerAndRowToolsButNoBannerUntilEdited() => RunAsync(async () =>
    {
        await using var f = await Fixture.CreateAsync();
        await f.RunAsync("SELECT * FROM t");
        Assert.False(f.Find<Border>("EditBar").IsVisible);
        Assert.True(f.Find<StackPanel>("RowTools").IsVisible);
        Assert.True(f.Find<StackPanel>("Marker").IsVisible);
        Assert.Equal("Modifiable · clé primaire id", f.Find<TextBlock>("MarkerText").Text);
        Assert.False(f.Find<Button>("IssuesButton").IsVisible);
        Assert.Equal("Lignes 1–2 sur 2 chargées", f.Find<TextBlock>("FooterText").Text);

        f.Tab.SelectedResult!.Editor!.TrySetCell(0, 1, "z");
        Assert.True(f.Find<Border>("EditBar").IsVisible);
        Assert.Equal("1 modification en attente", f.Find<TextBlock>("EditStatus").Text);
        Assert.Equal("1 mise à jour", f.Find<TextBlock>("EditDetail").Text);
    });

    [Fact]
    public Task ReadOnlyResult_ShowsTheReasonAndNoTools() => RunAsync(async () =>
    {
        await using var f = await Fixture.CreateAsync();
        await f.RunAsync("SELECT * FROM nopk");
        Assert.False(f.Find<Border>("EditBar").IsVisible);
        Assert.False(f.Find<StackPanel>("RowTools").IsVisible);
        Assert.True(f.Find<StackPanel>("Marker").IsVisible);
        Assert.StartsWith("Lecture seule · ", f.Find<TextBlock>("MarkerText").Text);
    });

    [Fact]
    public Task MessageResult_ShowsNothing() => RunAsync(async () =>
    {
        await using var f = await Fixture.CreateAsync();
        await f.RunAsync("UPDATE t SET name = name WHERE 1 = 0");
        Assert.False(f.Find<Border>("EditBar").IsVisible);
        Assert.False(f.Find<StackPanel>("Marker").IsVisible);
        Assert.False(f.Find<Border>("Footer").IsVisible);
    });

    [Fact]
    public Task ExportMenu_ItemsRunTheirExporter() => RunAsync(async () =>
    {
        await using var f = await Fixture.CreateAsync();
        await f.RunAsync("SELECT * FROM t");
        var menu = (StackPanel)((Flyout)f.Find<Button>("ExportButton").Flyout!).Content!;
        Assert.Equal("EXPORTER 2 LIGNES", ((TextBlock)menu.Children[0]).Text);
        var names = menu.Children.OfType<Button>().Select(b => b.Name).ToArray();
        Assert.Equal(["ExportCsv", "ExportJson"], names);
        foreach (var button in menu.Children.OfType<Button>())
            button.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(["csv", "json"], f.Extensions);
    });

    [Fact]
    public Task AddRow_AddsAPendingRowAndTheIssueDisablesCommit() => RunAsync(async () =>
    {
        await using var f = await Fixture.CreateAsync();
        await f.RunAsync("SELECT * FROM t");
        var editor = f.Tab.SelectedResult!.Editor!;
        f.Find<Button>("AddRow").RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.True(editor.HasPendingChanges);
        Assert.Equal(1, editor.PendingCount);
        // The NOT NULL name is still missing: the commit is refused and the issue is listed.
        Assert.False(f.Find<Button>("Commit").Command!.CanExecute(null));
        Assert.True(f.Find<Button>("IssuesButton").IsVisible);
        var item = Assert.Single(f.Find<ListBox>("IssueList").Items!.Cast<object>());
        Assert.StartsWith("Ligne 3", item.ToString());

        editor.TrySetCell(2, 1, "c");
        Assert.True(f.Find<Button>("Commit").Command!.CanExecute(null));
        Assert.False(f.Find<Button>("IssuesButton").IsVisible);
    });

    [Fact]
    public Task CtrlS_CommitsThePendingRow() => RunAsync(async () =>
    {
        await using var f = await Fixture.CreateAsync();
        await f.RunAsync("SELECT * FROM t");
        var result = f.Tab.SelectedResult!;
        result.EditController!.AddRow();
        result.Editor!.TrySetCell(2, 1, "c");
        f.Find<DataGridView>("ResultGrid").Canvas.Focus();
        f.Window.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.Control);
        await ((IAsyncRelayCommand)result.Editor.CommitCommand).ExecutionTask!;
        Assert.False(result.Editor.HasPendingChanges);
        Assert.Equal("1 modification validée", result.Editor.StatusText);
        await f.RunAsync("SELECT count(*) FROM t");
        Assert.Equal(3L, f.Tab.SelectedResult!.Grid!.GetCell(0, 0));
    });

    [Fact]
    public Task PreviewSql_RequestsTheGeneratedScript() => RunAsync(async () =>
    {
        await using var f = await Fixture.CreateAsync();
        await f.RunAsync("SELECT * FROM t");
        var result = f.Tab.SelectedResult!;
        result.Editor!.TrySetCell(0, 1, "z");
        f.Find<Button>("PreviewSql").Command!.Execute(null);
        var script = Assert.Single(f.Scripts);
        Assert.Contains("UPDATE", script);
    });
}
