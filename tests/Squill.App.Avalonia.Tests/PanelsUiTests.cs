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
            window.FindControl<RadioButton>("ThemeLight")!.IsChecked = true;
            Assert.Equal(ThemeMode.Light, settings.Theme);
            window.FindControl<RadioButton>("ThemeSystem")!.IsChecked = true;
            Assert.Equal(ThemeMode.System, settings.Theme);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task SettingsWindow_NavSwitchesPage()
    {
        await WithServicesAsync(services =>
        {
            var window = new SettingsWindow { DataContext = services.Settings, DataDirectory = "C:/data" };
            Assert.True(window.FindControl<StackPanel>("PageAppearance")!.IsVisible);
            Assert.False(window.FindControl<StackPanel>("PageGrid")!.IsVisible);

            window.FindControl<RadioButton>("NavGrid")!.IsChecked = true;
            Assert.False(window.FindControl<StackPanel>("PageAppearance")!.IsVisible);
            Assert.True(window.FindControl<StackPanel>("PageGrid")!.IsVisible);

            window.FindControl<RadioButton>("NavAdvanced")!.IsChecked = true;
            Assert.True(window.FindControl<StackPanel>("PageAdvanced")!.IsVisible);
            Assert.Equal("C:/data", window.FindControl<TextBlock>("DataFolderPath")!.Text);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task SettingsWindow_LanguageBoxListsThreeOptionsAndBindsLanguage()
    {
        await WithServicesAsync(services =>
        {
            var settings = services.Settings;
            var window = new SettingsWindow { DataContext = settings };
            var box = window.FindControl<ComboBox>("LanguageBox")!;

            Assert.Equal(
                [AppLanguage.System, AppLanguage.French, AppLanguage.English],
                box.Items.Cast<Squill.Presentation.Settings.LanguageOption>().Select(o => o.Value));

            var other = settings.ActiveLanguage == Squill.Presentation.Localization.UiLanguage.English ? AppLanguage.French : AppLanguage.English;
            box.SelectedValue = other;
            Assert.Equal(other, settings.Language);
            Assert.True(settings.RestartRequired);
            return Task.CompletedTask;
        });
    }
}
