using Avalonia.Controls;
using Avalonia.Input;
using Squill.App.Avalonia.Views;
using Squill.Core;
using Squill.Data;
using Squill.Data.Sqlite;
using Squill.Platform;
using Squill.Platform.Secrets;
using Squill.Presentation;
using Squill.Presentation.Explorer;
using Squill.Presentation.Localization;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class ExplorerMultiSelectUiTests
{
    private static readonly string[] MenuNames = ["NewItem", "DesignItem", "ScriptItem", "DropItem"];

    private sealed record Fixture(ObjectExplorerView View, TreeView Tree, ExplorerTreeViewModel Explorer, List<TablesRequest> Requests)
    {
        public ExplorerNode Find(string name) => Walk(Explorer.Roots).First(n => n.Title == name && n.Kind is ExplorerNodeKind.Table or ExplorerNodeKind.View);

        public MenuItem OpenMenu(ExplorerNode node, string item)
        {
            var menu = new ContextMenu { DataContext = node, ItemsSource = MenuNames.Select(n => new MenuItem { Name = n }).ToList() };
            View.OpenMenu(menu);
            return menu.Items.OfType<MenuItem>().Single(m => m.Name == item);
        }

        private static IEnumerable<ExplorerNode> Walk(IEnumerable<ExplorerNode> nodes) =>
            nodes.SelectMany(n => Walk(n.Children).Prepend(n));
    }

    private static async Task WithTree(Func<Fixture, Task> body)
    {
        var dir = Path.Combine(Path.GetTempPath(), "squill-multi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var profile = new ConnectionProfile(Guid.NewGuid(), "multi", DatabaseDialect.Sqlite, null, null, null, null, Path.Combine(dir, "multi.db"), null);
            await using (var seed = await new SqliteDatabaseProvider().OpenAsync(profile, null, CancellationToken.None))
            {
                foreach (var sql in new[] { "CREATE TABLE a (id INTEGER PRIMARY KEY)", "CREATE TABLE b (id INTEGER PRIMARY KEY)", "CREATE VIEW v AS SELECT id FROM a" })
                    await (await seed.ExecuteAsync(sql, [], CancellationToken.None)).DisposeAsync();
            }

            await Headless.Dispatch(async () =>
            {
                var providers = new ProviderRegistry();
                providers.Register(new SqliteDatabaseProvider());
                var services = AppComposition.Create(new TestPlatform(providers), new AppPaths(dir), secretStore: new InMemorySecretStore());
                var window = new MainWindow { DataContext = services.Workspace };
                window.Attach(services, dir);
                var view = window.FindControl<SidebarView>("Sidebar")!.FindControl<ObjectExplorerView>("ObjectExplorer")!;
                var explorer = services.Workspace.Explorer;
                var requests = new List<TablesRequest>();
                explorer.DropTablesRequested += (_, r) => requests.Add(r);
                services.Workspace.ConnectionList.Profiles.Add(profile);
                await services.Workspace.ConnectCommand.ExecuteAsync(profile);
                await body(new Fixture(view, view.FindControl<TreeView>("Tree")!, explorer, requests));
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
    public async Task Tree_selects_several_nodes_and_feeds_the_view_model() =>
        await WithTree(f =>
        {
            Assert.Equal(SelectionMode.Multiple, f.Tree.SelectionMode);
            f.Tree.SelectedItems!.Add(f.Find("a"));
            f.Tree.SelectedItems.Add(f.Find("b"));

            Assert.Equal(2, f.Explorer.SelectedTableCount);
            return Task.CompletedTask;
        });

    [Fact]
    public async Task Selecting_a_parent_does_not_add_its_children() =>
        await WithTree(f =>
        {
            var database = f.Explorer.Roots.Single().Children.First(n => n.Kind == ExplorerNodeKind.Database);
            f.Tree.SelectedItems!.Add(database);

            Assert.Equal(0, f.Explorer.SelectedTableCount);
            Assert.Equal([database], f.Explorer.SelectedNodes);
            return Task.CompletedTask;
        });

    [Fact]
    public async Task Programmatic_SelectedNode_change_keeps_the_multi_selection() =>
        await WithTree(f =>
        {
            f.Tree.SelectedItems!.Add(f.Find("a"));
            f.Tree.SelectedItems.Add(f.Find("b"));
            f.Explorer.SelectedNode = f.Find("a");

            Assert.Equal(2, f.Explorer.SelectedTableCount);
            return Task.CompletedTask;
        });

    [Fact]
    public async Task Menu_inside_a_selection_keeps_it_and_says_how_many_tables() =>
        await WithTree(f =>
        {
            f.Tree.SelectedItems!.Add(f.Find("a"));
            f.Tree.SelectedItems.Add(f.Find("b"));

            var drop = f.OpenMenu(f.Find("b"), "DropItem");

            Assert.Equal(2, f.Tree.SelectedItems.Count);
            Assert.Equal(UiText.FormatPlural("explorer.dropTables", 2), drop.Header);
            Assert.True(drop.IsEnabled);
            Assert.False(f.OpenMenu(f.Find("b"), "DesignItem").IsEnabled);
            Assert.False(f.OpenMenu(f.Find("b"), "ScriptItem").IsEnabled);
            return Task.CompletedTask;
        });

    [Fact]
    public async Task Menu_outside_the_selection_selects_that_node() =>
        await WithTree(f =>
        {
            f.Tree.SelectedItems!.Add(f.Find("a"));
            f.Tree.SelectedItems.Add(f.Find("b"));
            f.Tree.SelectedItems.Clear();

            var drop = f.OpenMenu(f.Find("a"), "DropItem");

            Assert.Same(f.Find("a"), f.Tree.SelectedItem);
            Assert.Equal(UiText.Get("explorer.dropTable"), drop.Header);
            Assert.True(f.OpenMenu(f.Find("a"), "DesignItem").IsEnabled);
            return Task.CompletedTask;
        });

    [Fact]
    public async Task Table_and_view_selected_disable_the_drop_entry() =>
        await WithTree(f =>
        {
            f.Tree.SelectedItems!.Add(f.Find("a"));
            f.Tree.SelectedItems.Add(f.Find("v"));

            Assert.False(f.OpenMenu(f.Find("a"), "DropItem").IsEnabled);
            return Task.CompletedTask;
        });

    [Fact]
    public async Task Delete_key_raises_one_drop_request_for_the_selected_tables() =>
        await WithTree(f =>
        {
            f.Tree.SelectedItems!.Add(f.Find("a"));
            f.Tree.SelectedItems.Add(f.Find("b"));

            f.Tree.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Delete });

            Assert.Equal(["a", "b"], Assert.Single(f.Requests).Tables.Select(t => t.Name));
            return Task.CompletedTask;
        });
}
