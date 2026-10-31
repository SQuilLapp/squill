using Avalonia.LogicalTree;
using Squill.App.Avalonia.Controls;
using Squill.App.Avalonia.Editors;
using Squill.Core;
using Squill.Data;
using Squill.Data.Sqlite;
using Squill.Platform;
using Squill.Presentation;
using Squill.Presentation.Editor;
using Squill.Presentation.Query;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class WorkspaceShellTests
{
    [Fact]
    public async Task Shell_RunSelectOne_ShowsGridInResultHost()
    {
        var dir = Path.Combine(Path.GetTempPath(), "squill-shell-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            await Headless.Session.Dispatch(async () =>
            {
                var providers = new ProviderRegistry();
                providers.Register(new SqliteDatabaseProvider());
                var services = AppComposition.Create(new TestPlatform(providers), new AppPaths(dir));
                var workspace = services.Workspace;
                // Not shown: the session disposes the fonts before its last render, so nothing may draw text.
                var window = new MainWindow { DataContext = workspace };

                var profile = new ConnectionProfile(Guid.NewGuid(), "test", DatabaseDialect.Sqlite, null, null, null, null, Path.Combine(dir, "t.db"), null);
                await workspace.ConnectCommand.ExecuteAsync(profile);
                var tab = workspace.ActiveTab!;
                tab.Editor.Text = "select 1";
                await tab.RunCommand.ExecuteAsync(null);

                Assert.Equal(ResultKind.Grid, tab.SelectedResult!.Kind);
                var grid = window.GetLogicalDescendants().OfType<DataGridView>().Single();
                Assert.Same(tab.SelectedResult.Grid, grid.DataSource);
                Assert.True(grid.IsVisible);
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
}
