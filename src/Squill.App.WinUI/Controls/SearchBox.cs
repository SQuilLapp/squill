using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Squill.App.WinUI.Controls;

/// <summary>A search field: leading magnifier glyph, borderless text box and an optional trailing keyboard chip (visual-spec section 3).</summary>
public sealed partial class SearchBox : UserControl
{
    private readonly TextBox _text;
    private readonly ContentControl _chip;
    private readonly Border _frame;

    /// <summary>Creates the search box.</summary>
    public SearchBox()
    {
        var resources = Application.Current.Resources;
        _text = new TextBox { BorderThickness = new Thickness(0), Padding = new Thickness(0), MinHeight = 0, VerticalAlignment = VerticalAlignment.Center };
        // The frame draws the background and the focus ring, so the inner box must not.
        foreach (var key in new[] { "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused", "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused" })
            _text.Resources[key] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        _text.Resources["TextControlBorderThemeThicknessFocused"] = new Thickness(0);
        _chip = new ContentControl { Style = (Style)resources["KeyboardChipStyle"], Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
        var glyph = new FontIcon { Glyph = "", FontSize = 12, Margin = new Thickness(0, 0, 8, 0) };
        glyph.Foreground = (Brush)resources["SquillFg3Brush"];
        var grid = new Grid { ColumnDefinitions = { new() { Width = GridLength.Auto }, new() { Width = new GridLength(1, GridUnitType.Star) }, new() { Width = GridLength.Auto } } };
        grid.Children.Add(glyph);
        Grid.SetColumn(_text, 1);
        grid.Children.Add(_text);
        Grid.SetColumn(_chip, 2);
        grid.Children.Add(_chip);
        _frame = new Border
        {
            MinHeight = (double)resources["SquillControlHeightStandard"],
            Padding = new Thickness(10, 0, 6, 0),
            CornerRadius = (CornerRadius)resources["SquillRadiusMd"],
            BorderThickness = new Thickness(1),
            Child = grid,
        };
        SyncBrushes(focused: false);
        _text.GotFocus += (_, _) => SyncBrushes(focused: true);
        _text.LostFocus += (_, _) => SyncBrushes(focused: false);
        ActualThemeChanged += (_, _) => SyncBrushes(_text.FocusState != FocusState.Unfocused);
        Content = _frame;
    }

    /// <summary>Gets the inner text box (for <c>TextChanged</c> and <c>Text</c>).</summary>
    public TextBox TextBox => _text;

    /// <summary>Gets or sets the placeholder shown while the field is empty.</summary>
    public string PlaceholderText
    {
        get => _text.PlaceholderText;
        set => _text.PlaceholderText = value;
    }

    /// <summary>Gets or sets the keyboard hint shown at the right (for example <c>Ctrl+K</c>); empty hides the chip.</summary>
    public string ChipText
    {
        get => _chip.Content as string ?? string.Empty;
        set
        {
            _chip.Content = value;
            _chip.Visibility = value.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private void SyncBrushes(bool focused)
    {
        var resources = Application.Current.Resources;
        _frame.Background = (Brush)resources["SquillBgRaisedBrush"];
        _frame.BorderBrush = (Brush)resources[focused ? "SquillFocusBrush" : "SquillBorderBrush"];
        _frame.BorderThickness = new Thickness(focused ? 2 : 1);
    }
}
