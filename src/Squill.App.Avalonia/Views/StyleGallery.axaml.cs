using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;

namespace Squill.App.Avalonia.Views;

/// <summary>Developer-only window that shows every shared control style and state; opened when SQUILL_STYLE_GALLERY=1.</summary>
public partial class StyleGallery : Window
{
    /// <summary>Initializes the gallery.</summary>
    public StyleGallery()
    {
        InitializeComponent();
        // SQUILL_STYLE_GALLERY_THEME=Light|Dark overrides the system theme (used by the screenshots).
        RequestedThemeVariant = Environment.GetEnvironmentVariable("SQUILL_STYLE_GALLERY_THEME") switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }

    private void OnLight(object? sender, RoutedEventArgs e) => RequestedThemeVariant = ThemeVariant.Light;

    private void OnDark(object? sender, RoutedEventArgs e) => RequestedThemeVariant = ThemeVariant.Dark;
}
