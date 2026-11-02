using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Squill.App.Avalonia.Views;
using Xunit;

namespace Squill.App.Avalonia.Tests;

[Collection("Headless")]
public sealed class StyleGalleryTests
{
    private static Task InGallery(ThemeVariant theme, Action<StyleGallery> body) => Headless.Session.Dispatch(() =>
    {
        var window = new StyleGallery { RequestedThemeVariant = theme };
        window.Show();
        try
        {
            body(window);
        }
        finally
        {
            window.Close();
        }
    }, TestContext.Current.CancellationToken);

    private static Color Token(StyleGallery window, string key)
    {
        Assert.True(window.TryFindResource(key, window.ActualThemeVariant, out var value));
        return Assert.IsAssignableFrom<ISolidColorBrush>(value).Color;
    }

    private static IBrush? Background(Button button) =>
        button.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter").Background;

    public static TheoryData<string> Themes => new() { "Light", "Dark" };

    [Theory]
    [MemberData(nameof(Themes))]
    public async Task Gallery_LoadsAndShowsEveryControl(string theme)
    {
        await InGallery(theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light, window =>
        {
            var visuals = window.GetVisualDescendants().ToList();
            Assert.NotEmpty(visuals.OfType<TextBox>());
            Assert.NotEmpty(visuals.OfType<ComboBox>());
            Assert.NotEmpty(visuals.OfType<CheckBox>());
            Assert.NotEmpty(visuals.OfType<ToggleSwitch>());
            Assert.NotEmpty(visuals.OfType<TabControl>());
            Assert.NotEmpty(visuals.OfType<ListBox>());
            Assert.NotEmpty(visuals.OfType<ProgressBar>());
        });
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public async Task PrimaryButton_ResolvesAccentBackground(string theme)
    {
        await InGallery(theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light, window =>
        {
            var button = window.FindControl<Button>("PrimaryButton")!;
            Assert.Equal(Token(window, "SquillAccentBrush"), Assert.IsAssignableFrom<ISolidColorBrush>(Background(button)).Color);
        });
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public async Task DisabledButton_ResolvesDisabledForeground(string theme)
    {
        await InGallery(theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light, window =>
        {
            var button = window.FindControl<Button>("PlainDisabledButton")!;
            var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");
            Assert.Equal(Token(window, "SquillFgDisabledBrush"), Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Foreground).Color);
        });
    }
}
