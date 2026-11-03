using Squill.Platform.Settings;

namespace Squill.App.WinUI.Views;

/// <summary>Choices of the settings window that do not need a UI thread.</summary>
internal static class SettingsFormat
{
    /// <summary>Gets the theme of each preview card, in the order drawn.</summary>
    public static ThemeMode[] Themes { get; } = [ThemeMode.System, ThemeMode.Light, ThemeMode.Dark];

    /// <summary>Gets the index of the preview card of a theme.</summary>
    /// <param name="theme">The theme.</param>
    /// <returns>The index in <see cref="Themes"/>.</returns>
    public static int ThemeIndex(ThemeMode theme) => Array.IndexOf(Themes, theme);
}
