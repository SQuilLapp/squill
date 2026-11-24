using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Squill.Presentation.Localization;

namespace Squill.App.WinUI.Views;

/// <summary>A dimmed layer with a centred "Pasting…" card and an indeterminate bar, shown over the result area during a long paste.</summary>
internal sealed partial class BusyOverlay : Grid
{
    private readonly Border _scrim = new() { Opacity = 0.6 };
    private readonly Border _card = new() { Padding = new Thickness(16), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _text = new() { Text = UiText.Get("results.pasting") };
    private int _count;

    /// <summary>Creates the hidden overlay.</summary>
    public BusyOverlay()
    {
        Visibility = Visibility.Collapsed;
        AutomationProperties.SetName(this, UiText.Get("results.pasting"));
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Assertive);
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(_text);
        stack.Children.Add(new ProgressBar { IsIndeterminate = true, Width = 220 });
        _card.Child = stack;
        Children.Add(_scrim);
        Children.Add(_card);
        // The element's own theme, as ResizeGrip does.
        void Paint()
        {
            _scrim.Background = Tokens.Brush(this, "SquillBgBaseBrush");
            _card.Background = Tokens.Brush(this, "SquillBgOverlayBrush");
            _card.BorderBrush = Tokens.Brush(this, "SquillBorderBrush");
            _text.Foreground = Tokens.Brush(this, "SquillFgBrush");
        }
        Loaded += (_, _) => Paint();
        ActualThemeChanged += (_, _) => Paint();
    }

    /// <summary>Shows the overlay; nested calls need as many <see cref="Hide"/> calls.</summary>
    public void Show()
    {
        _count++;
        Visibility = Visibility.Visible;
    }

    /// <summary>Hides the overlay once the last <see cref="Show"/> is matched.</summary>
    public void Hide()
    {
        if (_count > 0 && --_count == 0)
        {
            Visibility = Visibility.Collapsed;
        }
    }
}
