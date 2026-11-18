using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Squill.App.Avalonia.Views;
using Squill.Core;
using Squill.Data;
using Squill.Data.Sqlite;
using Squill.Platform;
using Squill.Platform.Profiles;
using Squill.Platform.Secrets;
using Squill.Presentation;
using Squill.Presentation.Home;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class HomePageUiTests
{
    private static ConnectionProfile Sqlite(string dir, string name, ConnectionEnvironment environment) =>
        new(Guid.NewGuid(), name, DatabaseDialect.Sqlite, null, null, null, null, Path.Combine(dir, name + ".db"), null) { Environment = environment };

    private static async Task WithHomeAsync(int cards, Func<MainWindow, AppServices, Task> body)
    {
        var dir = Path.Combine(Path.GetTempPath(), "squill-home-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            await Headless.Dispatch(async () =>
            {
                var paths = new AppPaths(dir);
                using (var profiles = new JsonProfileStore(paths, new InMemorySecretStore()))
                using (var recent = new JsonRecentConnectionsStore(paths))
                {
                    for (var i = 0; i < cards; i++)
                    {
                        var profile = Sqlite(dir, "db" + i, i == 0 ? ConnectionEnvironment.Production : ConnectionEnvironment.None);
                        await profiles.SaveAsync(profile, null, null, CancellationToken.None);
                        await recent.TouchAsync(profile.Id, DateTimeOffset.UtcNow.AddMinutes(-i), CancellationToken.None);
                    }
                }

                var providers = new ProviderRegistry();
                providers.Register(new SqliteDatabaseProvider());
                var services = AppComposition.Create(new TestPlatform(providers), paths, secretStore: new InMemorySecretStore());
                var window = new MainWindow { DataContext = services.Workspace, Width = 1280, Height = 720 };
                var loading = services.Workspace.ConnectionList.LoadCommand.ExecuteAsync(null);
                window.Attach(services, dir, loading);
                window.Show();
                await loading;
                await services.Workspace.InitializeHomeAsync(CancellationToken.None);
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

    private static HomePage Page(MainWindow window) => window.GetLogicalDescendants().OfType<HomePage>().Single();

    [Fact]
    public async Task Home_ShowsOneCardPerRecentConnection_AndHidesTheWorkspaceChrome()
    {
        await WithHomeAsync(2, (window, services) =>
        {
            var page = Page(window);
            Assert.True(page.IsVisible);
            Assert.False(window.FindControl<Border>("ActivityRail")!.IsVisible);
            Assert.False(window.FindControl<SidebarView>("Sidebar")!.IsVisible);
            Assert.False(window.FindControl<Grid>("MainArea")!.IsVisible);
            Assert.Equal(2, services.Workspace.Home.RecentCards.Count);
            Assert.Equal(2, page.FindControl<ItemsControl>("Cards")!.ItemCount);
            Assert.True(page.FindControl<ItemsControl>("Cards")!.IsVisible);
            Assert.False(page.FindControl<TextBlock>("EmptyText")!.IsVisible);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Home_WithoutRecentConnection_ShowsOneLineAndNoGrid()
    {
        await WithHomeAsync(0, (window, _) =>
        {
            var page = Page(window);
            Assert.True(page.FindControl<TextBlock>("EmptyText")!.IsVisible);
            Assert.False(page.FindControl<ItemsControl>("Cards")!.IsVisible);
            Assert.True(page.FindControl<Button>("NewConnectionButton")!.IsVisible);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Home_AcceleratorOpensTheFirstCard_AndLeavesTheHomePage()
    {
        await WithHomeAsync(2, async (window, services) =>
        {
            var page = Page(window);
            Assert.False(page.OpenCard(3));
            Assert.True(page.OpenCard(1));
            for (var i = 0; i < 200 && services.Workspace.IsHomeVisible; i++)
                await Task.Delay(25, TestContext.Current.CancellationToken);
            Assert.False(services.Workspace.IsHomeVisible);
            Assert.False(page.IsVisible);
            Assert.True(window.FindControl<Border>("ActivityRail")!.IsVisible);
        });
    }

    [Fact]
    public async Task Home_LongTipText_WrapsInsideTheTipCard()
    {
        await WithHomeAsync(1, async (window, services) =>
        {
            var page = Page(window);
            var tip = page.FindControl<ItemsControl>("TipText")!;
            var card = page.FindControl<Border>("TipCard")!;
            for (var i = 0; i < HomeTips.Keys.Count; i++)
            {
                window.UpdateLayout();
                Assert.True(tip.Bounds.Right <= card.Bounds.Width, $"tip {i}: {tip.Bounds.Right} > {card.Bounds.Width}");
                await services.Workspace.Home.NextTipCommand.ExecuteAsync(null);
            }
        });
    }

    [Fact]
    public async Task Home_RendersDifferentPixelsInLightAndDark()
    {
        await WithHomeAsync(2, (window, _) =>
        {
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
