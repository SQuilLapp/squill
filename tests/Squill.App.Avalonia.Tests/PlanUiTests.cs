using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Squill.App.Avalonia.Controls;
using Squill.App.Avalonia.Views;
using Squill.Core;
using Squill.Data;
using Squill.Data.Sqlite;
using Squill.Platform;
using Squill.Platform.Storage;
using Squill.Presentation;
using Squill.Presentation.Localization;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class PlanUiTests
{
    private static async Task WithPlanAsync(Func<MainWindow, AppServices, Task> body)
    {
        var dir = Path.Combine(Path.GetTempPath(), "squill-plan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            await Headless.Session.Dispatch(async () =>
            {
                var providers = new ProviderRegistry();
                providers.Register(new SqliteDatabaseProvider());
                var paths = new AppPaths(dir);
                using var store = new JsonDiagramLayoutStore(paths);
                var services = AppComposition.Create(new TestPlatform(providers), paths, store, ErDiagramCanvas.MeasureText);
                var window = new MainWindow { DataContext = services.Workspace, Width = 1280, Height = 720 };
                window.Attach(services, dir);
                window.Show();
                var profile = new ConnectionProfile(Guid.NewGuid(), "test", DatabaseDialect.Sqlite, null, null, null, null, Path.Combine(dir, "t.db"), null);
                await services.Workspace.ConnectCommand.ExecuteAsync(profile);
                var tab = services.Workspace.ActiveTab!;
                tab.Editor.Text = "create table customer (id integer primary key, name text);";
                await tab.RunAllCommand.ExecuteAsync(null);
                tab.Editor.Text = "select * from customer where name = 'a'";
                try
                {
                    await body(window, services);
                }
                finally
                {
                    window.Close();
                }
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public async Task Explain_OpensPlanTab_WithRawTextStripEntryAndStatusLine()
    {
        await WithPlanAsync(async (window, services) =>
        {
            var query = services.Workspace.ActiveTab!;
            var explain = window.GetLogicalDescendants().OfType<Button>().Single(b => b.Command == query.ExplainCommand);
            Assert.True(explain.IsEnabled);
            await query.ExplainCommand.ExecuteAsync(null);

            var plan = services.Workspace.ActivePlan!;
            var view = window.GetLogicalDescendants().OfType<PlanTabView>().Single();
            Assert.True(view.IsVisible);
            Assert.Same(plan, view.DataContext);
            var raw = view.FindControl<TextBox>("RawText")!;
            Assert.True(raw.IsReadOnly);
            Assert.False(string.IsNullOrWhiteSpace(raw.Text));
            Assert.Equal(plan.Plan.SourceText, raw.Text);
            var strip = window.FindControl<ListBox>("TabStrip")!;
            var entry = strip.Items.OfType<WorkspaceTab>().Single(t => t.Source == plan);
            Assert.Equal(UiText.Format("tab.plan", "").Trim(), entry.Suffix.Trim());
            Assert.Same(entry, strip.SelectedItem);
            Assert.Contains(plan.Plan.StatusLine, window.GetLogicalDescendants().OfType<StatusBar>().Single().GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

            // Selecting the query tab again hides the plan area.
            strip.SelectedItem = strip.Items.OfType<WorkspaceTab>().Single(t => t.Source == query);
            Assert.False(view.IsVisible);
            Assert.Null(services.Workspace.ActivePlan);
        });
    }

    [Fact]
    public async Task Analyze_OpensActualPlan_AndToolbarRendersInBothThemes()
    {
        await WithPlanAsync(async (window, services) =>
        {
            var query = services.Workspace.ActiveTab!;
            await query.AnalyzeCommand.ExecuteAsync(null);
            var plan = services.Workspace.ActivePlan!;
            Assert.Equal(Squill.Core.Plans.PlanMode.Actual, plan.Mode);
            var view = window.GetLogicalDescendants().OfType<PlanTabView>().Single();
            Assert.True(view.FindControl<Border>("RerunChip")!.IsVisible);

            // The toolbar paints with token brushes: the two themes must give different pixels.
            var frames = new List<byte[]>();
            foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                window.RequestedThemeVariant = variant;
                frames.Add(Render(window));
            }

            Assert.NotEqual(frames[0], frames[1]);
        });
    }

    [Fact]
    public async Task TreeView_ShowsTableAndDetails_FollowingSelection_AndTextViewShowsRawOnly()
    {
        await WithPlanAsync(async (window, services) =>
        {
            await services.Workspace.ActiveTab!.ExplainCommand.ExecuteAsync(null);
            var plan = services.Workspace.ActivePlan!.Plan;
            var view = window.GetLogicalDescendants().OfType<PlanTabView>().Single();
            plan.ViewMode = Squill.Presentation.Plans.PlanViewMode.Tree;
            await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, global::Avalonia.Threading.DispatcherPriority.Background);

            var texts = () => view.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.True(view.FindControl<Grid>("PlanBody")!.IsVisible);
            Assert.False(view.FindControl<TextBox>("RawText")!.IsVisible);
            foreach (var row in plan.Rows)
                Assert.Contains(row.Label, texts());

            plan.SelectedRow = plan.Rows[^1];
            await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, global::Avalonia.Threading.DispatcherPriority.Background);
            Assert.Contains(plan.Details.Title, texts());

            plan.ViewMode = Squill.Presentation.Plans.PlanViewMode.Text;
            await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, global::Avalonia.Threading.DispatcherPriority.Background);
            Assert.True(view.FindControl<TextBox>("RawText")!.IsVisible);
            Assert.False(view.FindControl<Grid>("PlanBody")!.IsVisible);
        });
    }

    private static byte[] Render(Window window)
    {
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

        return bgra;
    }
}
