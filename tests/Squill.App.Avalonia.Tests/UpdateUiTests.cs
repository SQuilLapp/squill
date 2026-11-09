using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.LogicalTree;
using Squill.App.Avalonia.Views;
using Squill.Core;
using Squill.Platform;
using Squill.Platform.Lifecycle;
using Squill.Platform.Updates;
using Squill.Presentation;
using Squill.Presentation.Localization;
using Squill.Presentation.Updates;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class UpdateUiTests
{
    private const string Sample = "### Added\n- **Fast** open of [the docs](https://example.com/docs) and `code`\n\nA paragraph with [bad](javascript:alert(1)) link.\n";

    private static async Task WithServices(Func<AppServices, Task> body)
    {
        var dir = Path.Combine(Path.GetTempPath(), "squill-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            await Headless.Session.Dispatch(
                async () => await body(AppComposition.Create(new TestPlatform(new Squill.Data.ProviderRegistry()), new AppPaths(dir))),
                TestContext.Current.CancellationToken);
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
    public async Task Presenter_builds_one_element_per_block_and_only_https_links_become_buttons() => await Headless.Session.Dispatch(() =>
    {
        var host = new Border();
        var stack = ReleaseNotesPresenter.Build(ReleaseNotes.Parse(Sample), host);

        Assert.Equal(3, stack.Children.Count);
        var links = stack.GetLogicalDescendants().OfType<SelectableTextBlock>()
            .SelectMany(t => t.Inlines!.OfType<InlineUIContainer>())
            .Select(i => i.Child)
            .OfType<Button>()
            .ToList();
        Assert.Single(links);
        Assert.Equal("the docs", links[0].Content);
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public async Task About_and_update_windows_open_without_an_installed_app() => await WithServices(services =>
    {
        var about = new AboutWindow(services.About);
        var update = new UpdateWindow(services.Updates);

        Assert.Equal(services.About.VersionText, about.FindControl<TextBlock>("VersionText")!.Text);
        Assert.Equal(services.Updates.StatusText, about.FindControl<TextBlock>("StatusText")!.Text);
        Assert.Equal(UiText.Get("update.status.notinstalled"), services.Updates.StatusText);
        Assert.False(about.FindControl<Button>("CheckButton")!.IsEnabled);
        Assert.False(update.FindControl<Button>("DownloadButton")!.IsVisible);
        about.Close();
        update.Close();
        return Task.CompletedTask;
    });

    [Fact]
    public async Task Settings_window_follows_the_update_status() => await WithServices(services =>
    {
        var window = new SettingsWindow { DataContext = services.Settings, Updates = services.Updates };

        Assert.Equal(services.Updates.StatusText, window.FindControl<TextBlock>("UpdateStatusText")!.Text);
        Assert.False(window.FindControl<Button>("CheckNowButton")!.IsEnabled);
        window.Close();
        return Task.CompletedTask;
    });

    [Fact]
    public async Task Main_window_hides_the_notice_until_an_update_exists() => await WithServices(services =>
    {
        var window = new MainWindow { DataContext = services.Workspace };
        window.Attach(services, Path.GetTempPath());

        Assert.False(window.UpdateBar.IsVisible);
        Assert.NotNull(window.FindControl<MenuItem>("AboutMenuItem"));
        return Task.CompletedTask;
    });
}
