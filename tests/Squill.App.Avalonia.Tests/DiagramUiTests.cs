using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Squill.App.Avalonia.Controls;
using Squill.App.Avalonia.Views;
using Squill.Core;
using Squill.Data;
using Squill.Data.Sqlite;
using Squill.Platform;
using Squill.Platform.Storage;
using Squill.Presentation;
using Squill.Presentation.Diagram;
using Squill.Presentation.Localization;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class DiagramUiTests
{
    private static async Task WithDiagramAsync(Func<MainWindow, AppServices, Task> body)
    {
        var dir = Path.Combine(Path.GetTempPath(), "squill-diagram-" + Guid.NewGuid().ToString("N"));
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
                var window = new MainWindow { DataContext = services.Workspace };
                window.Attach(services, dir);
                var profile = new ConnectionProfile(Guid.NewGuid(), "test", DatabaseDialect.Sqlite, null, null, null, null, Path.Combine(dir, "t.db"), null);
                await services.Workspace.ConnectCommand.ExecuteAsync(profile);
                var tab = services.Workspace.ActiveTab!;
                tab.Editor.Text = "create table customer (id integer primary key, name text not null);"
                                  + "create table orders (id integer primary key, customer_id integer not null references customer(id));";
                await tab.RunAllCommand.ExecuteAsync(null);
                await services.Workspace.ActiveSession!.RefreshSchemaAsync(CancellationToken.None);
                await body(window, services);
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
    public async Task OpenDiagram_ShowsViewBoundToTheDiagram_WithReachableExport()
    {
        await WithDiagramAsync(async (window, services) =>
        {
            await services.Workspace.OpenDiagramCommand.ExecuteAsync(null);
            var diagram = services.Workspace.ActiveDiagram!;
            var view = window.GetLogicalDescendants().OfType<DiagramTabView>().Single();
            Assert.True(view.IsVisible);
            Assert.Same(diagram, view.DiagramCanvas.Diagram);
            Assert.Equal(2, diagram.Model.Entities.Count);
            var export = view.GetLogicalDescendants().OfType<MenuItem>().Single(m => m.Header as string == UiText.Get("er.exportSvg"));
            Assert.Same(diagram.ExportSvgCommand, export.Command);
            Assert.True(export.Command!.CanExecute(null));
            var reset = view.GetLogicalDescendants().OfType<Button>().Single(b => b.Command == diagram.ResetLayoutCommand);
            Assert.NotNull(reset);
            Assert.Equal(2, view.FindControl<ListBox>("TableList")!.ItemCount);
            Assert.Contains("2 / 2", view.FindControl<TextBlock>("TablesHeader")!.Text);
        });
    }

    [Fact]
    public async Task Canvas_RendersTables_WithAccentPrimaryKeyTag()
    {
        await WithDiagramAsync(async (_, services) =>
        {
            await services.Workspace.OpenDiagramCommand.ExecuteAsync(null);
            var diagram = services.Workspace.ActiveDiagram!;
            var canvas = new ErDiagramCanvas { Diagram = diagram };
            var host = new Window { Width = 900, Height = 600, Content = canvas };
            host.Show();
            try
            {
                diagram.Zoom = 1;
                diagram.OffsetX = 20;
                diagram.OffsetY = 20;
                var bitmap = new RenderTargetBitmap(new PixelSize(900, 600));
                bitmap.Render(canvas);
                var accent = Application.Current!.TryFindResource("SquillAccentBrush", host.ActualThemeVariant, out var v) && v is ISolidColorBrush b ? b.Color : default;
                var box = diagram.Boxes[diagram.Model.Entities[0].Key];
                var row = diagram.Model.Entities[0].Columns.ToList().FindIndex(c => c.IsPrimaryKey);
                var rect = new PixelRect(
                    (int)(box.X + 20), (int)(box.Y + 20 + 33 + (24 * row)), 24, 24);
                var buffer = new byte[rect.Width * rect.Height * 4];
                var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
                try
                {
                    bitmap.CopyPixels(rect, handle.AddrOfPinnedObject(), buffer.Length, rect.Width * 4);
                }
                finally
                {
                    handle.Free();
                }

                var found = false;
                for (var i = 0; i < buffer.Length; i += 4)
                    found |= buffer[i + 2] == accent.R && buffer[i + 1] == accent.G && buffer[i] == accent.B;
                Assert.True(found, "no accent pixel in the PK tag");
            }
            finally
            {
                host.Close();
            }
        });
    }

    [Fact]
    public async Task Canvas_DragWheelAndDoubleClick_OperateTheDiagram()
    {
        await WithDiagramAsync(async (_, services) =>
        {
            await services.Workspace.OpenDiagramCommand.ExecuteAsync(null);
            var diagram = services.Workspace.ActiveDiagram!;
            var opened = new List<string>();
            diagram.OpenTableRequested += (_, t) => opened.Add(t.Name);
            var canvas = new ErDiagramCanvas { Diagram = diagram };
            var host = new Window { Width = 800, Height = 600, Content = canvas };
            host.Show();
            try
            {
                var key = diagram.Model.Entities[0].Key;
                var box = diagram.Boxes[key];
                var start = diagram.ToScreen(new ErPoint(box.X + 20, box.Y + 10));
                var from = new Point(start.X, start.Y);
                host.MouseDown(from, MouseButton.Left);
                host.MouseMove(from + new Vector(40, 25));
                host.MouseUp(from + new Vector(40, 25), MouseButton.Left);
                var moved = diagram.Boxes[key];
                Assert.Equal(box.X + 40 / diagram.Zoom, moved.X, 3);
                Assert.Equal(box.Y + 25 / diagram.Zoom, moved.Y, 3);
                Assert.Equal(key, diagram.SelectedKey);

                var zoom = diagram.Zoom;
                host.MouseWheel(new Point(100, 100), new Vector(0, 1));
                Assert.True(diagram.Zoom > zoom);

                var p = diagram.ToScreen(new ErPoint(moved.X + 20, moved.Y + 10));
                var at = new Point(p.X, p.Y);
                host.MouseDown(at, MouseButton.Left);
                host.MouseUp(at, MouseButton.Left);
                host.MouseDown(at, MouseButton.Left);
                host.MouseUp(at, MouseButton.Left);
                Assert.Single(opened);
            }
            finally
            {
                host.Close();
            }
        });
    }
}
