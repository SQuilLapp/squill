using Avalonia.Controls;
using Squill.App.Avalonia.Controls;
using Squill.App.Avalonia.Editors;
using Squill.App.Avalonia.Services;
using Squill.App.Avalonia.Views;
using Squill.Core;
using Squill.Data;
using Squill.Data.Sqlite;
using Squill.Platform.Settings;
using Squill.Platform.Storage;
using Squill.Presentation;
using Squill.Presentation.Localization;
using Squill.Presentation.Query;
using Squill.Presentation.Results;
using Squill.Presentation.Sessions;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class AutoRefreshButtonUiTests
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

    private static async Task WithViewAsync(Func<QueryTabView, QueryTabViewModel, Task> body)
    {
        var file = Path.Combine(Path.GetTempPath(), $"squill-autorefresh-{Guid.NewGuid():N}.db");
        await Headless.Session.Dispatch(async () =>
        {
            var profile = new ConnectionProfile(Guid.NewGuid(), "p", DatabaseDialect.Sqlite, null, null, null, null, file, null);
            await using var connection = await new SqliteDatabaseProvider().OpenAsync(profile, null, CancellationToken.None);
            var schema = await connection.ReadSchemaAsync(CancellationToken.None);
            var tab = new QueryTabViewModel(new ConnectionSession(profile, connection, schema, null), new AvaloniaSqlEditor { Text = "SELECT 1 AS a;" }, new History(), new Settings(), new UiThreadDispatcher(), new Dialogs());
            var view = new QueryTabView { DataContext = tab };
            var window = new Window { Width = 900, Height = 500, Content = view };
            window.Show();
            try
            {
                await body(view, tab);
            }
            finally
            {
                tab.SetAutoRefreshCommand.Execute(null);
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

    private static MenuItem[] Items(MenuFlyout menu) => menu.Items.OfType<MenuItem>().ToArray();

    [Fact]
    public Task Toolbar_HoldsTheClockRightBeforeStop() => WithViewAsync((view, _) =>
    {
        var panel = view.FindControl<StackPanel>("ToolbarLeft")!;
        var clock = view.FindControl<Button>("AutoRefreshButton")!;
        Assert.Equal(panel.Children.IndexOf(view.FindControl<Button>("StopButton")!) - 1, panel.Children.IndexOf(clock));
        return Task.CompletedTask;
    });

    [Fact]
    public Task Clock_IsDisabledBeforeAnyRun_WithTheUnavailableTooltip() => WithViewAsync((view, tab) =>
    {
        var clock = view.FindControl<Button>("AutoRefreshButton")!;
        Assert.False(clock.IsEnabled);
        Assert.True(ToolTip.GetShowOnDisabled(clock));
        Assert.Contains(UiText.Get("query.autoRefresh.unavailable"), (string)ToolTip.GetTip(clock)!);
        Assert.Equal(tab.AutoRefreshToolTip, ToolTip.GetTip(clock));
        return Task.CompletedTask;
    });

    [Fact]
    public Task Menu_ListsDisabledPresetsAndCustom_AndChoosingTenSecondsShowsItOnTheButton() => WithViewAsync(async (view, tab) =>
    {
        await tab.RunCommand.ExecuteAsync(null);
        var clock = view.FindControl<Button>("AutoRefreshButton")!;
        Assert.True(clock.IsEnabled);

        var menu = AutoRefreshMenu.Build(tab);
        var all = menu.Items.ToArray();
        Assert.Equal(9, all.Length);
        Assert.False(((MenuItem)all[0]!).IsEnabled);
        Assert.IsType<Separator>(all[7]);
        var items = Items(menu);
        Assert.Equal(UiText.Get("query.autoRefresh.disabled"), items[1].Header);
        Assert.True(items[1].IsChecked);
        Assert.Equal(QueryTabViewModel.AutoRefreshPresets.Select(QueryTabViewModel.FormatInterval), items.Skip(2).Take(5).Select(i => (string?)i.Header));
        Assert.Equal(UiText.Get("query.autoRefresh.custom"), items[^1].Header);

        items[3].Command!.Execute(items[3].CommandParameter);
        Assert.Equal(TimeSpan.FromSeconds(10), tab.AutoRefreshInterval);
        Assert.Equal("10 s", view.FindControl<TextBlock>("AutoRefreshLabel")!.Text);
        Assert.True(view.FindControl<TextBlock>("AutoRefreshLabel")!.IsVisible);
        Assert.True(Items(AutoRefreshMenu.Build(tab))[3].IsChecked);
    });

    [Fact]
    public Task Menu_ShowsACustomIntervalCheckedInFrench() => WithViewAsync(async (view, tab) =>
    {
        using var french = UiText.Override(UiLanguage.French);
        await tab.RunCommand.ExecuteAsync(null);
        tab.SetAutoRefreshCommand.Execute(TimeSpan.FromSeconds(90));
        Assert.True(tab.IsAutoRefreshCustom);
        var last = Items(AutoRefreshMenu.Build(tab))[^1];
        Assert.Equal($"Personnalisé ({tab.AutoRefreshLabel})...", last.Header);
        Assert.True(last.IsChecked);
    });

    [Fact]
    public Task Grid_SaveViewThenRestoreView_KeepsTheScrollAndTheChosenWidths() => WithViewAsync((view, _) =>
    {
        var canvas = view.FindControl<DataGridView>("ResultGrid")!.Canvas;
        canvas.Width = 400;
        canvas.Height = 200;
        canvas.Measure(new global::Avalonia.Size(400, 200));
        canvas.Arrange(new global::Avalonia.Rect(0, 0, 400, 200));
        canvas.DataSource = new FakeSource(100);
        canvas.ScrollY = 120;
        var memo = canvas.SaveView();
        Assert.Null(memo.Widths);

        canvas.DataSource = new FakeSource(100);
        Assert.Equal(0, canvas.ScrollY);
        canvas.RestoreView(memo);
        Assert.Equal(120, canvas.ScrollY);

        canvas.DataSource = new FakeSource(3);
        canvas.RestoreView(memo with { ScrollY = 5000 });
        Assert.Equal(canvas.MaxScrollY, canvas.ScrollY);

        canvas.RestoreView(new GridViewMemo(0, 0, [111, 222], 2));
        Assert.Equal(111, canvas.Layout.ColumnWidth(0));
        Assert.Equal(222, canvas.Layout.ColumnWidth(1));
        return Task.CompletedTask;
    });

    private sealed class FakeSource(long rows) : IGridDataSource
    {
        public IReadOnlyList<ColumnInfo> Columns { get; } = [new("a", "TEXT", typeof(string), false, 0), new("b", "TEXT", typeof(string), false, 1)];

        public long RowCount => rows;

        public bool IsComplete => true;

        public bool IsTruncated => false;

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public object? GetCell(long row, int column) => $"r{row}c{column}";
    }
}
