using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Squill.Presentation.Localization;

namespace Squill.App.WinUI.Localization;

/// <summary>Attached property <c>loc:Loc.Key</c> that fills a <see cref="TextBlock"/> text, a <see cref="TextBox"/> placeholder or a <see cref="ContentControl"/> content from the string catalog; plain dependency property, AOT-safe.</summary>
public static class Loc
{
    /// <summary>Catalog key of the element.</summary>
    public static readonly DependencyProperty KeyProperty =
        DependencyProperty.RegisterAttached("Key", typeof(string), typeof(Loc), new PropertyMetadata(null, OnKeyChanged));

    /// <summary>Gets the catalog key of an element.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The key, or <see langword="null"/>.</returns>
    public static string? GetKey(DependencyObject element) => (string?)element.GetValue(KeyProperty);

    /// <summary>Sets the catalog key of an element.</summary>
    /// <param name="element">The element.</param>
    /// <param name="value">The key.</param>
    public static void SetKey(DependencyObject element, string? value) => element.SetValue(KeyProperty, value);

    private static void OnKeyChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not string key)
            return;
        switch (element)
        {
            case TextBlock text:
                text.Text = UiText.Get(key);
                break;
            case TextBox box:
                box.PlaceholderText = UiText.Get(key);
                break;
            case ContentControl content:
                content.Content = UiText.Get(key);
                break;
        }
    }
}
