using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Squill.Presentation.Localization;

namespace Squill.App.WinUI.Localization;

/// <summary>
/// Attached properties that fill UI text from the string catalog; plain dependency properties, AOT-safe, resolved once when set at XAML load.
/// <c>Key</c>: <see cref="TextBlock"/> text, <see cref="TextBox"/> placeholder, <see cref="ContentControl"/> content.
/// <c>ToolTipKey</c>: tooltip of any element. <c>HeaderKey</c>: header of ComboBox, TextBox, NumberBox, ToggleSwitch, Expander, TabViewItem.
/// <c>TitleKey</c>: <see cref="MenuBarItem"/> and <see cref="ContentDialog"/> title. <c>MenuTextKey</c>: text of <see cref="MenuFlyoutItem"/> and <see cref="ToggleMenuFlyoutItem"/>.
/// <c>AutomationNameKey</c>: automation name of any element.
/// </summary>
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

    /// <summary>Catalog key of the tooltip of an element.</summary>
    public static readonly DependencyProperty ToolTipKeyProperty =
        DependencyProperty.RegisterAttached("ToolTipKey", typeof(string), typeof(Loc), new PropertyMetadata(null, OnToolTipKeyChanged));

    /// <summary>Gets the catalog key of the tooltip of an element.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The key, or <see langword="null"/>.</returns>
    public static string? GetToolTipKey(DependencyObject element) => (string?)element.GetValue(ToolTipKeyProperty);

    /// <summary>Sets the catalog key of the tooltip of an element.</summary>
    /// <param name="element">The element.</param>
    /// <param name="value">The key.</param>
    public static void SetToolTipKey(DependencyObject element, string? value) => element.SetValue(ToolTipKeyProperty, value);

    private static void OnToolTipKeyChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is string key)
            ToolTipService.SetToolTip(element, UiText.Get(key));
    }

    /// <summary>Catalog key of the header of a ComboBox, TextBox, NumberBox, ToggleSwitch, Expander or TabViewItem.</summary>
    public static readonly DependencyProperty HeaderKeyProperty =
        DependencyProperty.RegisterAttached("HeaderKey", typeof(string), typeof(Loc), new PropertyMetadata(null, OnHeaderKeyChanged));

    /// <summary>Gets the catalog key of the header of an element.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The key, or <see langword="null"/>.</returns>
    public static string? GetHeaderKey(DependencyObject element) => (string?)element.GetValue(HeaderKeyProperty);

    /// <summary>Sets the catalog key of the header of an element.</summary>
    /// <param name="element">The element.</param>
    /// <param name="value">The key.</param>
    public static void SetHeaderKey(DependencyObject element, string? value) => element.SetValue(HeaderKeyProperty, value);

    private static void OnHeaderKeyChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not string key)
            return;
        var text = UiText.Get(key);
        switch (element)
        {
            case ComboBox combo:
                combo.Header = text;
                break;
            case TextBox box:
                box.Header = text;
                break;
            case NumberBox number:
                number.Header = text;
                break;
            case ToggleSwitch toggle:
                toggle.Header = text;
                break;
            case Expander expander:
                expander.Header = text;
                break;
            case TabViewItem tab:
                tab.Header = text;
                break;
        }
    }

    /// <summary>Catalog key of the title of a <see cref="MenuBarItem"/> or <see cref="ContentDialog"/>.</summary>
    public static readonly DependencyProperty TitleKeyProperty =
        DependencyProperty.RegisterAttached("TitleKey", typeof(string), typeof(Loc), new PropertyMetadata(null, OnTitleKeyChanged));

    /// <summary>Gets the catalog key of the title of an element.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The key, or <see langword="null"/>.</returns>
    public static string? GetTitleKey(DependencyObject element) => (string?)element.GetValue(TitleKeyProperty);

    /// <summary>Sets the catalog key of the title of an element.</summary>
    /// <param name="element">The element.</param>
    /// <param name="value">The key.</param>
    public static void SetTitleKey(DependencyObject element, string? value) => element.SetValue(TitleKeyProperty, value);

    private static void OnTitleKeyChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not string key)
            return;
        switch (element)
        {
            case MenuBarItem item:
                item.Title = UiText.Get(key);
                break;
            case ContentDialog dialog:
                dialog.Title = UiText.Get(key);
                break;
        }
    }

    /// <summary>Catalog key of the text of a <see cref="MenuFlyoutItem"/> or <see cref="ToggleMenuFlyoutItem"/>.</summary>
    public static readonly DependencyProperty MenuTextKeyProperty =
        DependencyProperty.RegisterAttached("MenuTextKey", typeof(string), typeof(Loc), new PropertyMetadata(null, OnMenuTextKeyChanged));

    /// <summary>Gets the catalog key of the text of a menu item.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The key, or <see langword="null"/>.</returns>
    public static string? GetMenuTextKey(DependencyObject element) => (string?)element.GetValue(MenuTextKeyProperty);

    /// <summary>Sets the catalog key of the text of a menu item.</summary>
    /// <param name="element">The element.</param>
    /// <param name="value">The key.</param>
    public static void SetMenuTextKey(DependencyObject element, string? value) => element.SetValue(MenuTextKeyProperty, value);

    private static void OnMenuTextKeyChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is string key && element is MenuFlyoutItem item)
            item.Text = UiText.Get(key);
    }

    /// <summary>Catalog key of the automation name of an element.</summary>
    public static readonly DependencyProperty AutomationNameKeyProperty =
        DependencyProperty.RegisterAttached("AutomationNameKey", typeof(string), typeof(Loc), new PropertyMetadata(null, OnAutomationNameKeyChanged));

    /// <summary>Gets the catalog key of the automation name of an element.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The key, or <see langword="null"/>.</returns>
    public static string? GetAutomationNameKey(DependencyObject element) => (string?)element.GetValue(AutomationNameKeyProperty);

    /// <summary>Sets the catalog key of the automation name of an element.</summary>
    /// <param name="element">The element.</param>
    /// <param name="value">The key.</param>
    public static void SetAutomationNameKey(DependencyObject element, string? value) => element.SetValue(AutomationNameKeyProperty, value);

    private static void OnAutomationNameKeyChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is string key)
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(element, UiText.Get(key));
    }
}
