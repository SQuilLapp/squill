using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
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
public sealed class ExplorerFilterPopupUiTests
{
    private sealed record Fixture(ObjectExplorerView View, ExplorerTreeViewModel Explorer)
    {
        // SQLite names its one schema "main": the tables sit under it, so it carries the table badge.
        public ExplorerNode Database => Walk(Explorer.Roots).First(n => n.Kind == ExplorerNodeKind.Schema);

        public static IEnumerable<ExplorerNode> Walk(IEnumerable<ExplorerNode> nodes) => nodes.SelectMany(n => Walk(n.Children).Prepend(n));

        public List<Button> Badges()
        {
            Dispatcher.UIThread.RunJobs();
            return [.. View.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("filter-badge") && b.IsVisible)];
        }
    }

    private static async Task WithTree(Func<Fixture, Task> body)
    {
        var dir = Path.Combine(Path.GetTempPath(), "squill-filter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var profile = new ConnectionProfile(Guid.NewGuid(), "filter", DatabaseDialect.Sqlite, null, null, null, null, Path.Combine(dir, "filter.db"), null);
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
                window.Show();
                var view = window.FindControl<SidebarView>("Sidebar")!.FindControl<ObjectExplorerView>("ObjectExplorer")!;
                services.Workspace.ConnectionList.Profiles.Add(profile);
                await services.Workspace.ConnectCommand.ExecuteAsync(profile);
                try
                {
                    await body(new Fixture(view, services.Workspace.Explorer));
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

    private static List<string> RowTexts(Flyout flyout) =>
        [.. ((Control)flyout.Content!).GetLogicalDescendants().OfType<CheckBox>().Select(c => AutomationProperties.GetName(c) ?? "")];

    [Fact]
    public async Task Database_row_shows_the_badge_and_the_popup_lists_the_tables() =>
        await WithTree(f =>
        {
            Assert.Equal(UiText.Format("explorer.filterBadge", "3", "3"), f.Database.FilterBadge);
            Assert.Contains(f.Badges(), b => Equals(b.Content, f.Database.FilterBadge));

            var vm = f.Explorer.CreateFilter(f.Database)!;
            var flyout = ExplorerFilterMenu.Show(f.View, vm);
            var texts = RowTexts(flyout);
            Assert.Equal(4, texts.Count);
            Assert.Contains("a", texts);
            Assert.Contains("v", texts);
            flyout.Hide();
            return Task.CompletedTask;
        });

    [Fact]
    public async Task Unchecking_a_table_hides_it_and_updates_the_badge() =>
        await WithTree(f =>
        {
            var flyout = ExplorerFilterMenu.Show(f.View, f.Explorer.CreateFilter(f.Database)!);
            var box = ((Control)flyout.Content!).GetLogicalDescendants().OfType<CheckBox>().First(c => AutomationProperties.GetName(c) == "a");
            box.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            Assert.DoesNotContain(Fixture.Walk(f.Explorer.Roots), n => n.Kind == ExplorerNodeKind.Table && n.Title == "a");
            Assert.Equal(UiText.Format("explorer.filterBadge", "2", "3"), f.Database.FilterBadge);
            Assert.False(box.IsChecked);
            flyout.Hide();
            return Task.CompletedTask;
        });

    [Fact]
    public async Task Context_menu_offers_choose_shown_on_the_database_and_not_on_a_column() =>
        await WithTree(f =>
        {
            string[] names = ["ChooseShownItem", "ShowAllItem"];
            MenuItem Open(ExplorerNode node)
            {
                var menu = new ContextMenu { DataContext = node, ItemsSource = names.Select(n => new MenuItem { Name = n }).ToList() };
                f.View.OpenMenu(menu);
                return menu.Items.OfType<MenuItem>().First(m => m.Name == "ChooseShownItem");
            }

            Assert.True(Open(f.Database).IsVisible);
            var table = Fixture.Walk(f.Explorer.Roots).First(n => n.Kind == ExplorerNodeKind.Table);
            Assert.False(Open(table).IsVisible);
            return Task.CompletedTask;
        });

    [Fact]
    public async Task Rows_without_a_filter_show_no_badge() =>
        await WithTree(f =>
        {
            Assert.All(Fixture.Walk(f.Explorer.Roots).Where(n => n.Kind is ExplorerNodeKind.Column or ExplorerNodeKind.Table), n => Assert.False(n.HasFilterBadge));
            return Task.CompletedTask;
        });
}
