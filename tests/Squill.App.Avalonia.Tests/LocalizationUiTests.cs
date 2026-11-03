using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Squill.App.Avalonia.Views;
using Squill.Data;
using Squill.Platform;
using Squill.Presentation;
using Squill.Presentation.Connections;
using Squill.Presentation.Localization;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed partial class LocalizationUiTests
{
    [GeneratedRegex(@"^!.+!$")]
    private static partial Regex MissingKey();

    private static IEnumerable<string> TextsOf(Control root) =>
        root.GetLogicalDescendants().Prepend(root).Select(c => c switch
        {
            TextBlock t => t.Text,
            ContentControl { Content: string s } => s,
            _ => null,
        }).OfType<string>();

    [Theory]
    [InlineData(UiLanguage.English)]
    [InlineData(UiLanguage.French)] // last: leaves any language-dependent static state in the default language for the other tests
    public async Task MainViews_ShowNoMissingKeyMarker(UiLanguage language)
    {
        var dir = Path.Combine(Path.GetTempPath(), "squill-loc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            await Headless.Session.Dispatch(async () =>
            {
                using var _ = UiText.Override(language);
                var services = AppComposition.Create(new TestPlatform(new ProviderRegistry()), new AppPaths(dir));
                Control[] views =
                [
                    new MainWindow { DataContext = services.Workspace },
                    new SettingsWindow { DataContext = services.Settings },
                    new ConnectionEditorWindow { DataContext = new ConnectionEditorViewModel(null, new ConnectionUiTests.NullStore(), new ProviderRegistry(), new Dialogs()) },
                ];

                foreach (var view in views)
                {
                    var texts = TextsOf(view).ToList();
                    Assert.NotEmpty(texts);
                    Assert.DoesNotContain(texts, t => MissingKey().IsMatch(t));
                }

                await Task.CompletedTask;
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            // AppComposition.Create applies the OS language process-wide; put the test default (French) back for the other classes.
            UiText.Initialize(UiLanguage.French);
            CultureInfo.DefaultThreadCurrentCulture = UiText.Culture;
            CultureInfo.DefaultThreadCurrentUICulture = UiText.Culture;
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
