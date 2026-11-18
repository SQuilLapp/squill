using Avalonia.Controls;
using Avalonia.Threading;
using Squill.App.Avalonia.Views;
using Squill.Core;
using Squill.Data;
using Squill.Data.Sqlite;
using Squill.Platform;
using Squill.Presentation;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class StatusBarThreadTests
{
    [Fact]
    public async Task WorkspaceEventRaisedOnWorkerThread_RefreshesOnUiThreadWithoutThrowing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "squill-statusthread-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Exception? failure = null;
        bool? visibleBefore = null, visibleAfter = null;
        try
        {
            await Headless.Session.Dispatch(async () =>
            {
                var providers = new ProviderRegistry();
                providers.Register(new SqliteDatabaseProvider());
                var workspace = AppComposition.Create(new TestPlatform(providers), new AppPaths(dir)).Workspace;
                var bar = new StatusBar { DataContext = workspace };
                var profile = new ConnectionProfile(Guid.NewGuid(), "test", DatabaseDialect.Sqlite, null, null, null, null, Path.Combine(dir, "t.db"), null, ConnectionEnvironment.Production);
                await workspace.ConnectCommand.ExecuteAsync(profile);
                Dispatcher.UIThread.RunJobs();

                // Dispatch swallows exceptions of an async body: the verdict is read outside.
                var block = bar.FindControl<Border>("EnvironmentBlock")!;
                visibleBefore = block.IsVisible;
                try
                {
                    // ShowTab raises ActiveEnvironment, which the status bar listens to; here from a worker thread.
                    await Task.Run(() => workspace.ShowTab(null), TestContext.Current.CancellationToken);
                    Dispatcher.UIThread.RunJobs();
                    visibleAfter = block.IsVisible;
                }
                catch (Exception ex)
                {
                    failure = ex;
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

        Assert.Null(failure);
        Assert.True(visibleBefore);
        Assert.True(visibleAfter);
    }
}