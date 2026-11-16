using Avalonia.Controls;
using Avalonia.Headless;
using Squill.App.Avalonia.Editors;
using Squill.App.Avalonia.Views;
using Squill.Core;
using Squill.Data;
using Squill.Data.Sqlite;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation;
using Squill.Presentation.Query;
using Squill.Presentation.Results;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class FilterBarUiTests
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

    private static readonly string[] Setup = ["CREATE TABLE t (id INTEGER PRIMARY KEY, name TEXT NOT NULL)", "INSERT INTO t (name) VALUES ('a'), ('b')"];

    private static async Task RunAsync(Func<QueryTabView, QueryTabViewModel, Task> body)
    {
        var file = Path.Combine(Path.GetTempPath(), $"squill-filter-{Guid.NewGuid():N}.db");
        await Headless.Session.Dispatch(async () =>
        {
            var profile = new ConnectionProfile(Guid.NewGuid(), "p", DatabaseDialect.Sqlite, null, null, null, null, file, null);
            await using var connection = await new SqliteDatabaseProvider().OpenAsync(profile, null, CancellationToken.None);
            foreach (var sql in Setup)
                await (await connection.ExecuteAsync(sql, [], CancellationToken.None)).DisposeAsync();
            var schema = await connection.ReadSchemaAsync(CancellationToken.None);
            var tab = new QueryTabViewModel(new ConnectionSession(profile, connection, schema, null), new AvaloniaSqlEditor(), new History(), new Settings(), new ImmediateDispatcher(), new Dialogs());
            var view = new QueryTabView { DataContext = tab };
            var window = new Window { Width = 900, Height = 500, Content = view };
            window.Show();
            try
            {
                tab.Editor.Text = "SELECT * FROM t";
                await tab.RunCommand.ExecuteAsync(null);
                await body(view, tab);
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken);
        try
        {
            File.Delete(file);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public Task Filter_ShowsOneChip_AndTheCrossRemovesIt() => RunAsync((view, tab) =>
    {
        var bar = view.FindControl<FilterBar>("FilterBar")!;
        Assert.True(bar.IsVisible);
        var chips = bar.FindControl<StackPanel>("Chips")!;
        Assert.Empty(chips.Children);

        var state = tab.SelectedResult!.View!;
        state.AddFilter(new GridFilter(1, GridFilterOperator.Equals, "a", null));

        var chip = Assert.Single(chips.Children);
        Assert.Contains("a", state.Filters[0].Text);
        Assert.Contains(state.Filters[0].Text, ((TextBlock)((StackPanel)((Border)chip).Child!).Children[1]).Text);
        ((Button)((StackPanel)((Border)chip).Child!).Children[2]).RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Empty(state.Filters);
        Assert.Empty(chips.Children);
        return Task.CompletedTask;
    });

    [Fact]
    public Task SearchBox_SetsSearchTextAfterTheDelay() => RunAsync(async (view, tab) =>
    {
        var bar = view.FindControl<FilterBar>("FilterBar")!;
        var state = tab.SelectedResult!.View!;
        bar.FindControl<TextBox>("Search")!.Text = "b";
        Assert.False(bar.FindControl<Border>("Shortcut")!.IsVisible);

        await Task.Delay(400);
        global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal("b", state.SearchText);
    });

    [Fact]
    public Task PendingChanges_DisableTheBar() => RunAsync((view, tab) =>
    {
        var bar = view.FindControl<FilterBar>("FilterBar")!;
        Assert.True(bar.FindControl<Button>("AddFilter")!.IsEnabled);

        tab.SelectedResult!.Editor!.TrySetCell(0, 1, "z");

        Assert.False(bar.FindControl<Button>("AddFilter")!.IsEnabled);
        Assert.False(bar.FindControl<TextBox>("Search")!.IsEnabled);
        return Task.CompletedTask;
    });
}
