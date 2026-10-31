using Avalonia.LogicalTree;
using Avalonia.Controls;
using Squill.App.Avalonia.Views;
using Squill.Core;
using Squill.Data;
using Squill.Platform;
using Squill.Platform.Settings;
using Squill.Presentation;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class PanelsUiTests
{
    private static async Task WithServicesAsync(Func<AppServices, Task> body)
    {
        var dir = Path.Combine(Path.GetTempPath(), "squill-panels-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            await Headless.Session.Dispatch(
                () => body(AppComposition.Create(new TestPlatform(new ProviderRegistry()), new AppPaths(dir))),
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
    public async Task Sidebar_SwitchingSegments_ChangesVisiblePanel()
    {
        await WithServicesAsync(services =>
        {
            var window = new MainWindow { DataContext = services.Workspace };
            window.Attach(services, Path.GetTempPath());
            var sidebar = window.GetLogicalDescendants().OfType<SidebarView>().Single();
            var history = sidebar.GetLogicalDescendants().OfType<HistoryView>().Single();
            var snippets = sidebar.GetLogicalDescendants().OfType<SnippetsView>().Single();
            Assert.True(sidebar.FindControl<Grid>("ExplorerPanel")!.IsVisible);
            Assert.False(history.IsVisible);

            sidebar.FindControl<RadioButton>("HistoryTab")!.IsChecked = true;
            Assert.True(history.IsVisible);
            Assert.False(snippets.IsVisible);
            Assert.False(sidebar.FindControl<Grid>("ExplorerPanel")!.IsVisible);

            sidebar.FindControl<RadioButton>("SnippetsTab")!.IsChecked = true;
            Assert.True(snippets.IsVisible);
            Assert.False(history.IsVisible);

            sidebar.FindControl<RadioButton>("ExplorerTab")!.IsChecked = true;
            Assert.True(sidebar.FindControl<Grid>("ExplorerPanel")!.IsVisible);
            Assert.False(snippets.IsVisible);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task SettingsWindow_BindsTheFourProperties()
    {
        await WithServicesAsync(services =>
        {
            var settings = services.Settings;
            var window = new SettingsWindow { DataContext = settings };

            settings.EditorFontSize = 18;
            settings.MaxGridRows = 5000;
            settings.FormatterKeywordCase = KeywordCase.Lower;
            Assert.Equal(18m, window.FindControl<NumericUpDown>("FontSizeBox")!.Value);
            Assert.Equal(5000m, window.FindControl<NumericUpDown>("MaxRowsBox")!.Value);
            Assert.Equal(KeywordCase.Lower, window.FindControl<ComboBox>("KeywordCaseBox")!.SelectedItem);

            window.FindControl<NumericUpDown>("FontSizeBox")!.Value = 14;
            Assert.Equal(14d, settings.EditorFontSize);
            window.FindControl<RadioButton>("ThemeDark")!.IsChecked = true;
            Assert.Equal(ThemeMode.Dark, settings.Theme);
            return Task.CompletedTask;
        });
    }
}
