using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Squill.App.WinUI.Views;

/// <summary>Developer-only window that shows every shared control style and state; opened when SQUILL_STYLE_GALLERY=1.</summary>
public sealed partial class StyleGalleryWindow : Window
{
    /// <summary>Creates the gallery.</summary>
    public StyleGalleryWindow()
    {
        InitializeComponent();
        var tables = new TreeViewNode { Content = "Tables", IsExpanded = true };
        tables.Children.Add(new TreeViewNode { Content = "clients" });
        tables.Children.Add(new TreeViewNode { Content = "commandes" });
        Tree.RootNodes.Add(tables);
        // SQUILL_STYLE_GALLERY_THEME=Light|Dark overrides the system theme (used by the screenshots).
        Root.RequestedTheme = Environment.GetEnvironmentVariable("SQUILL_STYLE_GALLERY_THEME") switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        Root.ActualThemeChanged += (_, _) => CaptionBar.Paint(AppWindow.TitleBar, Root, false);
        Root.Loaded += (_, _) => CaptionBar.Paint(AppWindow.TitleBar, Root, false);
    }

    /// <summary>Gets whether the environment asks for the gallery instead of the app.</summary>
    /// <returns><see langword="true"/> when <c>SQUILL_STYLE_GALLERY=1</c>.</returns>
    public static bool IsRequested() => Environment.GetEnvironmentVariable("SQUILL_STYLE_GALLERY") == "1";

    private void OnLight(object sender, RoutedEventArgs e) => Root.RequestedTheme = ElementTheme.Light;

    private void OnDark(object sender, RoutedEventArgs e) => Root.RequestedTheme = ElementTheme.Dark;

    private async void OnDialog(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = "Confirmer",
            Content = "Dialog frame",
            PrimaryButtonText = "Valider",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            RequestedTheme = Root.RequestedTheme,
        };
        await dialog.ShowAsync();
    }
}
