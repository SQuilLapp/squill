using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Squill.App.Avalonia.Controls;
using Squill.App.Avalonia.Editors;
using Squill.Core;
using Squill.Data;
using Squill.Data.Sqlite;
using Squill.Platform;
using Squill.Platform.Storage;
using Squill.Presentation;
using Squill.Presentation.Palette;
using Squill.Presentation.Workspace;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class CommandPaletteUiTests
{
    private static readonly string[] TestActions = ["zzalpha", "zzbeta"];

    private static async Task WithWindowAsync(Func<MainWindow, PaletteViewModel, CommandPalette, Task> body)
    {
        var dir = Path.Combine(Path.GetTempPath(), "squill-palette-" + Guid.NewGuid().ToString("N"));
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
                var window = new MainWindow { DataContext = services.Workspace, Width = 1100, Height = 700 };
                window.Attach(services, dir);
                var profile = new ConnectionProfile(Guid.NewGuid(), "test", DatabaseDialect.Sqlite, null, null, null, null, Path.Combine(dir, "t.db"), null);
                await services.Workspace.ConnectCommand.ExecuteAsync(profile);
                window.Show();
                try
                {
                    await body(window, services.Workspace.Palette, window.GetVisualDescendants().OfType<CommandPalette>().Single());
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

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None) => window.KeyPressQwerty(key, modifiers);

    private static async Task WaitAsync(Func<bool> done)
    {
        for (var i = 0; i < 100 && !done(); i++)
            await Task.Delay(20, TestContext.Current.CancellationToken);
        Assert.True(done());
    }

    [Fact]
    public async Task CtrlK_OpensFocusesTheTextBoxAndTogglesClosed()
    {
        await WithWindowAsync((window, vm, palette) =>
        {
            Assert.False(palette.IsVisible);
            Press(window, PhysicalKey.K, RawInputModifiers.Control);
            Assert.True(vm.IsOpen);
            Assert.True(palette.IsVisible);
            Assert.True(palette.GetVisualDescendants().OfType<TextBox>().Single().IsFocused);
            Press(window, PhysicalKey.K, RawInputModifiers.Control);
            Assert.False(vm.IsOpen);
            Assert.False(palette.IsVisible);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Typing_ShowsSections_AndDownEnterRunsTheSecondRow()
    {
        await WithWindowAsync(async (window, vm, _) =>
        {
            var ran = new List<string>();
            foreach (var id in TestActions)
                vm.Registry.Add(new PaletteAction(id, id, "menu.tools", null, "action", () => true, _ =>
                {
                    ran.Add(id);
                    return ValueTask.CompletedTask;
                }));
            Press(window, PhysicalKey.K, RawInputModifiers.Control);
            window.KeyTextInput("comm");
            await WaitAsync(() => vm.Sections.Count > 0);
            Assert.Equal("comm", vm.Text);
            vm.Text = "zz";
            await WaitAsync(() => vm.Sections.Sum(s => s.Items.Count) == 2);
            Press(window, PhysicalKey.ArrowDown);
            Assert.Equal(1, vm.SelectedIndex);
            var expected = vm.Selected!.Title;
            Press(window, PhysicalKey.Enter);
            await WaitAsync(() => ran.Count == 1);
            Assert.Equal(expected, ran[0]);
            Assert.False(vm.IsOpen);
        });
    }

    [Fact]
    public async Task Escape_ClosesWithoutRunning_AndFocusReturnsToTheEditor()
    {
        await WithWindowAsync((window, vm, _) =>
        {
            var editor = (AvaloniaSqlEditor)((WorkspaceViewModel)window.DataContext!).ActiveTab!.Editor;
            editor.FocusEditor();
            var focused = window.FocusManager!.GetFocusedElement();
            Assert.NotNull(focused);
            var before = editor.Text;
            Press(window, PhysicalKey.K, RawInputModifiers.Control);
            window.KeyTextInput("q");
            Assert.Equal("q", vm.Text);
            Assert.Equal(before, editor.Text);
            Press(window, PhysicalKey.Escape);
            Assert.False(vm.IsOpen);
            Assert.Same(focused, window.FocusManager!.GetFocusedElement());
            Assert.Equal(before, editor.Text);
            return Task.CompletedTask;
        });
    }
}
