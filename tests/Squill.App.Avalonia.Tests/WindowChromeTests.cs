using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Squill.App.Avalonia.Views;
using Squill.Core;
using Squill.Data;
using Squill.Data.Sqlite;
using Squill.Platform;
using Squill.Presentation;
using Squill.Presentation.Localization;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class WindowChromeTests
{
    private static async Task WithWindow(Func<MainWindow, Squill.Presentation.Workspace.WorkspaceViewModel, string, Task> body)
    {
        var dir = Path.Combine(Path.GetTempPath(), "squill-chrome-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            await Headless.Session.Dispatch(async () =>
            {
                var providers = new ProviderRegistry();
                providers.Register(new SqliteDatabaseProvider());
                var workspace = AppComposition.Create(new TestPlatform(providers), new AppPaths(dir)).Workspace;
                // Not shown: the session disposes the fonts before its last render, so nothing may draw text.
                await body(new MainWindow { DataContext = workspace }, workspace, dir);
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
    public async Task NarrowTitleBar_FoldsTheMenuIntoTheHamburgerAndBack() => await WithWindow((window, _, _) =>
    {
        var count = window.MainMenu.Items.Count;
        Assert.True(count > 3);

        window.SetMenuCollapsed(true);
        Assert.Empty(window.MainMenu.Items);
        Assert.True(window.HamburgerButton.IsVisible);
        Assert.False(window.MainMenu.IsVisible);
        Assert.Equal(count, ((MenuFlyout)window.HamburgerButton.Flyout!).Items.Count);

        window.SetMenuCollapsed(false);
        Assert.Equal(count, window.MainMenu.Items.Count);
        Assert.False(window.HamburgerButton.IsVisible);
        Assert.True(window.MainMenu.IsVisible);
        return Task.CompletedTask;
    });

    private static ConnectionProfile Profile(string dir, ConnectionEnvironment environment) =>
        new(Guid.NewGuid(), "test", DatabaseDialect.Sqlite, null, null, null, null, Path.Combine(dir, "t.db"), null, environment);

    private static MenuItem Item(MainWindow window, string key) =>
        window.GetLogicalDescendants().OfType<MenuItem>().First(m => m.Header as string == UiText.Get(key));

    [Fact]
    public async Task Menu_Items_AreBoundToWorkspaceCommands() =>
        await WithWindow(async (window, workspace, dir) =>
        {
            await workspace.ConnectCommand.ExecuteAsync(Profile(dir, ConnectionEnvironment.None));
            var tab = workspace.ActiveTab!;

            Assert.Same(workspace.NewTabCommand, Item(window, "menu.newTab").Command);
            Assert.Same(workspace.CloseTabCommand, Item(window, "menu.closeTab").Command);
            Assert.Same(workspace.OpenDiagramCommand, Item(window, "menu.erDiagram").Command);
            Assert.Same(workspace.NewTableCommand, Item(window, "menu.newTable").Command);
            Assert.Same(tab.RunCommand, Item(window, "editor.run").Command);
            Assert.Same(tab.RunAllCommand, Item(window, "menu.runScript").Command);
            Assert.Same(tab.CancelCommand, Item(window, "editor.stop").Command);
            Assert.Same(tab.FormatDocumentCommand, Item(window, "menu.format").Command);
            Assert.False(Item(window, "menu.undo").IsEnabled);
            Assert.False(window.FindControl<Border>("CommandSearch")!.IsEnabled);
        });

    [Fact]
    public async Task StatusBar_EnvironmentBlock_HiddenForNone() =>
        await WithWindow(async (window, workspace, dir) =>
        {
            var status = window.FindControl<StatusBar>("Status")!;
            var block = status.FindControl<Border>("EnvironmentBlock")!;
            Assert.False(block.IsVisible);

            await workspace.ConnectCommand.ExecuteAsync(Profile(dir, ConnectionEnvironment.None));
            Assert.False(block.IsVisible);
        });

    [Theory]
    [InlineData(ConnectionEnvironment.Production, "SquillEnvProdBrush", "status.production", true)]
    [InlineData(ConnectionEnvironment.Staging, "SquillEnvStagingBrush", "status.staging", false)]
    [InlineData(ConnectionEnvironment.Development, "SquillEnvDevBrush", "status.dev", false)]
    [InlineData(ConnectionEnvironment.Local, "SquillEnvLocalBrush", "status.local", false)]
    public async Task StatusBar_EnvironmentBlock_ShowsEnvironmentBrush(ConnectionEnvironment environment, string brushKey, string labelKey, bool warning) =>
        await WithWindow(async (window, workspace, dir) =>
        {
            await workspace.ConnectCommand.ExecuteAsync(Profile(dir, environment));

            var status = window.FindControl<StatusBar>("Status")!;
            var block = status.FindControl<Border>("EnvironmentBlock")!;
            Assert.True(block.IsVisible);
            Assert.True(status.TryFindResource(brushKey, status.ActualThemeVariant, out var expected));
            Assert.Equal(((ISolidColorBrush)expected!).Color, ((ISolidColorBrush)block.Background!).Color);
            Assert.Equal(UiText.Get(labelKey), status.FindControl<TextBlock>("EnvironmentLabel")!.Text);
            Assert.Equal(warning, status.FindControl<Control>("EnvironmentWarning")!.IsVisible);
        });

    [Fact]
    public async Task Rail_Selection_SwitchesLeftPanel() =>
        await WithWindow((window, _, _) =>
        {
            var sidebar = window.FindControl<SidebarView>("Sidebar")!;
            var history = sidebar.GetLogicalDescendants().OfType<HistoryView>().Single();
            var snippets = sidebar.GetLogicalDescendants().OfType<SnippetsView>().Single();
            var explorer = sidebar.FindControl<Control>("ExplorerPanel")!;
            Assert.True(explorer.IsVisible);

            window.FindControl<RadioButton>("RailHistory")!.IsChecked = true;
            Assert.True(history.IsVisible);
            Assert.False(explorer.IsVisible);

            window.FindControl<RadioButton>("RailSnippets")!.IsChecked = true;
            Assert.True(snippets.IsVisible);
            Assert.False(history.IsVisible);

            window.FindControl<RadioButton>("RailExplorer")!.IsChecked = true;
            Assert.True(explorer.IsVisible);
            Assert.False(snippets.IsVisible);
            return Task.CompletedTask;
        });
}
