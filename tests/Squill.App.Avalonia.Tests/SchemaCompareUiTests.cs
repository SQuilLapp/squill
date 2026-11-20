using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Squill.App.Avalonia.Controls;
using Squill.App.Avalonia.Views;
using Squill.Core;
using Squill.Data;
using Squill.Data.Sqlite;
using Squill.Platform;
using Squill.Presentation;
using Squill.Presentation.Compare;
using Squill.Presentation.Localization;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class SchemaCompareUiTests
{
    private static ConnectionProfile Sqlite(string dir, string name, ConnectionEnvironment environment) =>
        new(Guid.NewGuid(), name, DatabaseDialect.Sqlite, null, null, null, null, Path.Combine(dir, name + ".db"), null) { Environment = environment };

    private static async Task RunAsync(Squill.Presentation.Workspace.WorkspaceViewModel workspace, string sql)
    {
        workspace.ActiveTab!.Editor.Text = sql;
        await workspace.ActiveTab.RunAllCommand.ExecuteAsync(null);
    }

    // Source: a(id, name, note) and b; target: a(id, name) and old, as the view-model tests do.
    private static async Task WithCompareAsync(ConnectionEnvironment targetEnvironment, Func<MainWindow, SchemaCompareView, SchemaCompareViewModel, Task> body)
    {
        var dir = Path.Combine(Path.GetTempPath(), "squill-cmp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            await Headless.Session.Dispatch(async () =>
            {
                var providers = new ProviderRegistry();
                providers.Register(new SqliteDatabaseProvider());
                var services = AppComposition.Create(new TestPlatform(providers), new AppPaths(dir));
                var window = new MainWindow { DataContext = services.Workspace, Width = 1280, Height = 800 };
                window.Attach(services, dir);
                window.Show();
                var workspace = services.Workspace;
                await workspace.ConnectCommand.ExecuteAsync(Sqlite(dir, "dev", ConnectionEnvironment.Development));
                await RunAsync(workspace, "create table a (id integer primary key, name text, note text); create table b (id integer primary key);");
                await workspace.ConnectCommand.ExecuteAsync(Sqlite(dir, "prod", targetEnvironment));
                await RunAsync(workspace, "create table a (id integer primary key, name text); create table old (id integer primary key);");
                try
                {
                    workspace.OpenSchemaCompareCommand.Execute(null);
                    var compare = Assert.Single(workspace.Compares);
                    var view = window.GetLogicalDescendants().OfType<SchemaCompareView>().Single();
                    compare.Source = compare.Sources.First(e => e.Session.Profile.Name == "dev");
                    compare.Target = compare.Targets.First(e => e.Session.Profile.Name == "prod");
                    await compare.CompareCommand.ExecuteAsync(null);
                    await compare.ScriptTask;
                    await body(window, view, compare);
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
    public void Row_MarksAddedAndRemovedLinesAndHatchesTheMissingSide()
    {
        var added = new AlignedLine(1, "new", null, null, LineMark.SourceOnly);
        var removed = new AlignedLine(null, null, 1, "old", LineMark.TargetOnly);
        var changed = new AlignedLine(2, "n", 2, "o", LineMark.Changed);

        Assert.Equal(DdlRowKind.Added, CompareFormat.Row(added, true).Kind);
        Assert.Equal(DdlRowKind.Filler, CompareFormat.Row(added, false).Kind);
        Assert.Equal(DdlRowKind.Filler, CompareFormat.Row(removed, true).Kind);
        Assert.Equal(DdlRowKind.Removed, CompareFormat.Row(removed, false).Kind);
        Assert.Equal(DdlRowKind.Added, CompareFormat.Row(changed, true).Kind);
        Assert.Equal(DdlRowKind.Removed, CompareFormat.Row(changed, false).Kind);
        Assert.Equal("+", CompareFormat.Mark(DdlRowKind.Added));
        Assert.Equal("-", CompareFormat.Mark(DdlRowKind.Removed));
        Assert.Equal("", CompareFormat.Mark(DdlRowKind.Filler));
    }

    [Fact]
    public async Task StatusBarEnvironment_FollowsTheTargetAfterTheEndpointsChange()
    {
        await WithCompareAsync(ConnectionEnvironment.Production, async (window, _, compare) =>
        {
            var workspace = (Squill.Presentation.Workspace.WorkspaceViewModel)window.DataContext!;
            var label = window.FindControl<StatusBar>("Status")!.FindControl<TextBlock>("EnvironmentLabel")!;
            compare.Source = compare.Sources.First(e => e.Session.Profile.Name == "prod");
            compare.Target = compare.Targets.First(e => e.Session.Profile.Name == "dev");

            compare.SwapCommand.Execute(null);
            await compare.CompareCommand.ExecuteAsync(null);

            Assert.Equal(ConnectionEnvironment.Production, workspace.ActiveEnvironment);
            Assert.Equal(UiText.Get("status.production"), label.Text);
        });
    }

    [Fact]
    public async Task Panes_ShowTheSameRowCountOnBothSides_ForTheSelectedItem()
    {
        await WithCompareAsync(ConnectionEnvironment.Staging, (_, view, compare) =>
        {
            compare.SelectedItem = compare.Groups.Single().Items.First(i => i.Name.EndsWith(".a", StringComparison.Ordinal) || i.Name == "a");
            var panes = view.GetLogicalDescendants().OfType<DdlPane>().ToList();

            Assert.Equal(2, panes.Count);
            Assert.NotEmpty(compare.Lines);
            Assert.All(panes, p => Assert.Equal(compare.Lines.Count, p.RowCount));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ExecuteButton_FollowsTheTargetEnvironment()
    {
        await WithCompareAsync(ConnectionEnvironment.Production, (_, view, compare) =>
        {
            var button = view.GetLogicalDescendants().OfType<Button>().Single(b => b.Classes.Contains("outline-danger"));
            Assert.Equal(UiText.Format("compare.executeIn", UiText.Get("status.production")), button.Content);
            Assert.Equal(compare.ExecuteScriptCommand.CanExecute(null), button.IsEnabled);
            return Task.CompletedTask;
        });
        await WithCompareAsync(ConnectionEnvironment.Staging, (_, view, _) =>
        {
            var button = view.GetLogicalDescendants().OfType<Button>().Single(b => b.Classes.Contains("primary"));
            Assert.Equal(UiText.Get("compare.execute"), button.Content);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Compare_RendersDifferentPixelsInLightAndDark()
    {
        await WithCompareAsync(ConnectionEnvironment.Staging, (window, _, compare) =>
        {
            compare.SelectedItem = compare.Groups.Single().Items[0];
            var frames = new List<byte[]>();
            foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                window.RequestedThemeVariant = variant;
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
}
