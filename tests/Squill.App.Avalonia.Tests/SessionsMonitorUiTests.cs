using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Squill.App.Avalonia.Views;
using Squill.Core;
using Squill.Core.Activity;
using Squill.Data;
using Squill.Presentation;
using Squill.Presentation.Activity;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class SessionsMonitorUiTests
{
    private static ServerSession Session(string id, SessionState state, double seconds, bool system = false) =>
        new(id, "app", "psql", "10.0.0.1:5000", state, state.ToString(), TimeSpan.FromSeconds(seconds), null, "select " + id, null, null, null, system);

    private sealed class Reader(int extraRows = 0) : IActivityReader
    {
        public bool SupportsCancelQuery => true;

        public ValueTask<ActivitySnapshot> ReadAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ActivitySnapshot(
                DateTimeOffset.UtcNow, 3, 100, null,
                [Session("1", SessionState.IdleInTransaction, 400), Session("2", SessionState.Waiting, 90), Session("3", SessionState.Active, 1), Session("4", SessionState.Active, 1, system: true), .. Enumerable.Range(0, extraRows).Select(i => Session("x" + i, SessionState.Active, 1))],
                [new BlockingEdge("1", "2", null, null)], SourceName: "pg_stat_activity"));

        public ValueTask CancelQueryAsync(string sessionId, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask TerminateSessionAsync(string sessionId, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private static async Task WithViewAsync(Func<Window, SessionsMonitorView, SessionsMonitorViewModel, Task> body, int extraRows = 0)
    {
        await Headless.Session.Dispatch(async () =>
        {
            var profile = new ConnectionProfile(Guid.NewGuid(), "main", DatabaseDialect.PostgreSql, "h", 1, "appdb", "u", null, null, ConnectionEnvironment.Staging);
            using var vm = new SessionsMonitorViewModel(profile, new Reader(extraRows), new Dialogs(), new ImmediateDispatcher(), TimeProvider.System);
            await vm.RefreshAsync();
            var view = new SessionsMonitorView { DataContext = vm };
            var window = new Window { Content = view, Width = 1280, Height = 800 };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                await body(window, view, vm);
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken);
    }

    private static List<Border> RowElements(SessionsMonitorView view) =>
        [.. view.GetLogicalDescendants().OfType<Border>().Where(b => b.Focusable)];

    [Theory]
    [InlineData(StateDotKind.Success, "SquillSuccessBrush")]
    [InlineData(StateDotKind.Warning, "SquillWarningBrush")]
    [InlineData(StateDotKind.Danger, "SquillDangerBrush")]
    [InlineData(StateDotKind.Muted, "SquillFg3Brush")]
    public void State_dots_use_the_token_of_their_kind(StateDotKind kind, string brush) =>
        Assert.Equal(brush, SessionsFormat.StateBrush(kind));

    [Fact]
    public void Durations_arrows_and_sparkline_follow_the_view_model_values()
    {
        Assert.Equal("SquillFgBrush", SessionsFormat.DurationBrush(ActivitySeverity.Normal));
        Assert.Equal("SquillWarningBrush", SessionsFormat.DurationBrush(ActivitySeverity.Warning));
        Assert.Equal("SquillDangerBrush", SessionsFormat.DurationBrush(ActivitySeverity.Danger));
        Assert.Equal("↓", SessionsFormat.SortArrow(true));
        Assert.Equal("↑", SessionsFormat.SortArrow(false));
        Assert.Empty(SessionsFormat.Sparkline([5]));
        var points = SessionsFormat.Sparkline([0, 10, 5]);
        Assert.Equal((0.0, 100.0), points[0]);
        Assert.Equal((50.0, 0.0), points[1]);
    }

    [Fact]
    public async Task Table_has_one_row_per_visible_session_and_the_filter_tabs_show_the_counts()
    {
        await WithViewAsync((_, view, vm) =>
        {
            Assert.Equal(vm.Rows.Count, RowElements(view).Count);
            Assert.Equal(3, vm.Rows.Count);
            var tabs = view.GetLogicalDescendants().OfType<ToggleButton>().ToList();
            Assert.Equal(4, tabs.Count);
            Assert.Equal([vm.AllTabText, vm.ActiveTabText, vm.IdleInTransactionTabText, vm.WaitingTabText], tabs.Select(t => t.Content));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Clicking_a_row_selects_the_session_by_id_and_highlights_it()
    {
        await WithViewAsync((_, view, vm) =>
        {
            var id = vm.Rows[1].Id;
            vm.SelectSession(id);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(id, vm.SelectedSession?.Id);
            Assert.NotNull(vm.Detail);
            var rows = RowElements(view);
            Assert.NotEqual(rows[0].Background, rows[1].Background);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Monitor_renders_different_pixels_in_light_and_dark()
    {
        await WithViewAsync((window, _, vm) =>
        {
            vm.SelectSession(vm.Rows[0].Id);
            var frames = new List<byte[]>();
            foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                window.RequestedThemeVariant = variant;
                Dispatcher.UIThread.RunJobs();
                var size = new PixelSize((int)window.Width, (int)window.Height);
                var bitmap = new RenderTargetBitmap(size);
                bitmap.Render(window);
                var bgra = new byte[size.Width * size.Height * 4];
                var handle = System.Runtime.InteropServices.GCHandle.Alloc(bgra, System.Runtime.InteropServices.GCHandleType.Pinned);
                try
                {
                    bitmap.CopyPixels(new PixelRect(size), handle.AddrOfPinnedObject(), bgra.Length, size.Width * 4);
                }
                finally
                {
                    handle.Free();
                }

                frames.Add(bgra);
            }

            Assert.NotEqual(frames[0], frames[1]);
            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData(397, false)]
    [InlineData(398, true)]
    public async Task The_rows_cut_notice_shows_only_beyond_the_limit(int extraRows, bool cut)
    {
        await WithViewAsync((_, view, vm) =>
        {
            Assert.Equal(cut, vm.HiddenRowCount > 0);
            var notices = view.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Name == "RowsCutNotice").ToList();
            Assert.Equal(cut ? 1 : 0, notices.Count);
            if (cut)
                Assert.Equal(vm.RowsCutText, notices[0].Text);
            return Task.CompletedTask;
        }, extraRows);
    }
}
