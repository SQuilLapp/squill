using Avalonia.Controls;
using Squill.App.Avalonia.Views;
using Squill.Core;
using Squill.Data;
using Squill.Data.Sqlite;
using Squill.Platform;
using Squill.Presentation;
using Squill.Presentation.Designer;
using Squill.Presentation.Explorer;
using Squill.Presentation.Localization;
using Squill.Presentation.Workspace;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class ExplorerAndTabsTests
{
    private static async Task WithApp(Func<MainWindow, WorkspaceViewModel, string, Task> body)
    {
        var dir = Path.Combine(Path.GetTempPath(), "squill-tabs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            await Headless.Session.Dispatch(async () =>
            {
                var providers = new ProviderRegistry();
                providers.Register(new SqliteDatabaseProvider());
                var services = AppComposition.Create(new TestPlatform(providers), new AppPaths(dir));
                // Not shown: the session disposes the fonts before its last render, so nothing may draw text.
                var window = new MainWindow { DataContext = services.Workspace };
                window.Attach(services, dir);
                await body(window, services.Workspace, dir);
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

    private static ConnectionProfile Profile(string dir, string name = "test", ConnectionEnvironment environment = ConnectionEnvironment.None, string? folder = null) =>
        new(Guid.NewGuid(), name, DatabaseDialect.Sqlite, null, null, null, null, Path.Combine(dir, name + ".db"), null, environment, folder);

    [Fact]
    public async Task Sidebar_ShowsOneTree_WithFoldersAndConnections() =>
        await WithApp((window, workspace, dir) =>
        {
            var tree = window.FindControl<SidebarView>("Sidebar")!.FindControl<ObjectExplorerView>("ObjectExplorer")!.FindControl<TreeView>("Tree")!;
            Assert.Same(workspace.Explorer.Roots, tree.ItemsSource);
            Assert.Empty(workspace.Explorer.Roots);

            workspace.ConnectionList.Profiles.Add(Profile(dir, "one", folder: "Dossier"));
            workspace.ConnectionList.Profiles.Add(Profile(dir, "two"));

            Assert.Equal([ExplorerNodeKind.ConnectionFolder, ExplorerNodeKind.Connection], workspace.Explorer.Roots.Select(r => r.Kind));
            return Task.CompletedTask;
        });

    [Fact]
    public async Task Tree_SelectionFollowsConnections_AndConnectedNodeShowsDatabase() =>
        await WithApp(async (window, workspace, dir) =>
        {
            var tree = window.FindControl<SidebarView>("Sidebar")!.FindControl<ObjectExplorerView>("ObjectExplorer")!.FindControl<TreeView>("Tree")!;
            var profile = Profile(dir, "lite");
            workspace.ConnectionList.Profiles.Add(profile);
            var node = workspace.Explorer.Roots.Single();
            Assert.Empty(node.Children);

            tree.SelectedItem = node;
            Assert.Same(profile, workspace.ConnectionList.SelectedProfile);
            Assert.Null(workspace.ActiveSession);

            await workspace.ConnectCommand.ExecuteAsync(profile);

            Assert.True(node.IsConnected);
            Assert.True(node.IsExpanded);
            Assert.Equal(ExplorerNodeKind.Database, Assert.Single(node.Children).Kind);
            tree.SelectedItem = node;
            Assert.Same(node.Session, workspace.ActiveSession);
        });

    [Fact]
    public async Task TabList_MergesQueriesThenDesigners_InOrder_AndCloses() =>
        await WithApp(async (_, workspace, dir) =>
        {
            var tabs = new WorkspaceTabList(workspace);
            await workspace.ConnectCommand.ExecuteAsync(Profile(dir, environment: ConnectionEnvironment.Production));
            workspace.NewTableCommand.Execute(null);
            workspace.NewTabCommand.Execute(null);

            Assert.Equal(3, tabs.Items.Count);
            Assert.IsType<Squill.Presentation.Query.QueryTabViewModel>(tabs.Items[0].Source);
            Assert.IsType<Squill.Presentation.Query.QueryTabViewModel>(tabs.Items[1].Source);
            Assert.IsType<TableDesignerViewModel>(tabs.Items[2].Source);
            Assert.All(tabs.Items, t => Assert.True(t.IsProd));
            Assert.Same(tabs.Items[1], tabs.Selected);

            tabs.Selected = tabs.Items[2];
            Assert.Same(workspace.ActiveDesigner, tabs.Items[2].Source);

            tabs.Close(tabs.Items[0]);
            Assert.Equal(2, tabs.Items.Count);
            Assert.Same(workspace.ActiveTab, tabs.Items[0].Source);

            tabs.Close(tabs.Items[1]);
            Assert.Single(tabs.Items);
            Assert.Same(tabs.Items[0], tabs.Selected);
        });

    [Fact]
    public async Task TabList_ShowsDirtyDot_WhenScriptIsEdited() =>
        await WithApp(async (_, workspace, dir) =>
        {
            var tabs = new WorkspaceTabList(workspace);
            await workspace.ConnectCommand.ExecuteAsync(Profile(dir));
            var tab = tabs.Items.Single();
            Assert.False(tab.IsDirty);
            Assert.False(tab.IsProd);

            workspace.ActiveTab!.Editor.Text = "select 1";

            Assert.True(tab.IsDirty);
        });

    [Fact]
    public async Task PropertiesPanel_ShowsOnlyForTableOrView_WithColumnsAndIndexes() =>
        await WithApp((window, workspace, _) =>
        {
            var sidebar = window.FindControl<SidebarView>("Sidebar")!;
            var panel = window.FindControl<PropertiesPanel>("Properties")!;
            Assert.False(panel.IsVisible);

            var table = new TableInfo(
                "public",
                "clients",
                TableKind.Table,
                [new("id", "integer", false, true, null), new("pays_id", "integer", true, false, null), new("nom", "text", true, false, null)],
                [new("idx_nom", false, ["nom"])],
                [new("fk_pays", ["pays_id"], null, "pays", ["id"], ReferentialAction.NoAction, ReferentialAction.NoAction)]);
            var node = new ExplorerNode("clients", ExplorerNodeKind.Table, table: table);
            panel.Show(node, workspace);

            Assert.True(panel.IsVisible);
            var rows = Assert.IsAssignableFrom<IEnumerable<ColumnRow>>(panel.FindControl<ItemsControl>("ColumnList")!.ItemsSource).ToList();
            Assert.Equal(["id", "pays_id", "nom"], rows.Select(r => r.Name));
            Assert.Equal(["◆", "→", "·"], rows.Select(r => r.Glyph));
            Assert.Equal(UiText.Format("properties.columns", 3), panel.FindControl<TextBlock>("ColumnsHeader")!.Text);
            Assert.Equal(UiText.Format("properties.indexes", 1), panel.FindControl<TextBlock>("IndexesHeader")!.Text);
            Assert.Equal("Table · public", panel.FindControl<TextBlock>("SubtitleText")!.Text);

            sidebar.FindControl<ObjectExplorerView>("ObjectExplorer")!.FindControl<TreeView>("Tree")!.SelectedItem = new ExplorerNode("c", ExplorerNodeKind.Column);
            Assert.False(panel.IsVisible);
            return Task.CompletedTask;
        });

    [Fact]
    public void ExplorerGlyphs_CoverEveryObjectKind() =>
        Assert.All(
            new[]
            {
                (ExplorerNodeKind.MaterializedView, "mv"),
                (ExplorerNodeKind.Function, "fn"),
                (ExplorerNodeKind.Procedure, "pr"),
                (ExplorerNodeKind.Trigger, "tr"),
            },
            x =>
            {
                Assert.Equal(x.Item2, ExplorerGlyphs.Glyph.Convert(x.Item1, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture));
                Assert.True(ExplorerGlyphs.IsPlain.Convert(x.Item1, typeof(bool), null, System.Globalization.CultureInfo.InvariantCulture) as bool?);
            });

    [Fact]
    public async Task ContextMenu_OffersDefinitionAndScriptCommands_ForTheRightNodes() =>
        await WithApp((window, _, _) =>
        {
            var view = window.FindControl<SidebarView>("Sidebar")!.FindControl<ObjectExplorerView>("ObjectExplorer")!;
            var menu = new ContextMenu
            {
                Items = { new MenuItem { Name = "ScriptItem" }, new MenuItem { Name = "DefinitionItem" } },
            };
            bool[] Visible(ExplorerNodeKind kind)
            {
                menu.DataContext = new ExplorerNode("x", kind);
                view.OpenMenu(menu);
                return [.. menu.Items.OfType<MenuItem>().Select(i => i.IsVisible)];
            }

            Assert.Equal([true, false], Visible(ExplorerNodeKind.Table));
            Assert.Equal([false, true], Visible(ExplorerNodeKind.Function));
            Assert.Equal([false, true], Visible(ExplorerNodeKind.Trigger));
            return Task.CompletedTask;
        });
}
