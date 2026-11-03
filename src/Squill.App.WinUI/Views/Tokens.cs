using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Squill.App.WinUI.Views;

/// <summary>Reads design tokens for the theme an element is shown in, for code that builds its visuals by hand.</summary>
internal static class Tokens
{
    /// <summary>Gets a token color for the actual theme of an element.</summary>
    /// <param name="element">The element, whose <see cref="FrameworkElement.ActualTheme"/> decides the variant.</param>
    /// <param name="brushKey">A token brush key such as <c>SquillFgBrush</c>; the color <c>SquillFgColor</c> is read.</param>
    /// <returns>The color of the light or the dark variant.</returns>
    public static Color Color(FrameworkElement element, string brushKey)
    {
        var variant = element.ActualTheme == ElementTheme.Light ? "Light" : "Default";
        var color = brushKey[..^"Brush".Length] + "Color";
        return Find(Application.Current.Resources, variant, color) ?? throw new KeyNotFoundException(color);
    }

    // The token dictionary is merged into the application resources, so its theme dictionaries sit one or more levels down.
    private static Color? Find(ResourceDictionary dictionary, string variant, string color)
    {
        if (dictionary.ThemeDictionaries.TryGetValue(variant, out var theme) && ((ResourceDictionary)theme).TryGetValue(color, out var value))
            return (Color)value;
        foreach (var merged in dictionary.MergedDictionaries)
        {
            if (Find(merged, variant, color) is { } found)
                return found;
        }

        return null;
    }

    /// <summary>Gets a token brush for the actual theme of an element.</summary>
    /// <param name="element">The element, whose actual theme decides the variant.</param>
    /// <param name="brushKey">A token brush key such as <c>SquillFgBrush</c>.</param>
    /// <returns>A new solid brush; call again after the theme changed.</returns>
    public static Brush Brush(FrameworkElement element, string brushKey) => new SolidColorBrush(Color(element, brushKey));
}
