using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Squill.Presentation.Localization;

namespace Squill.App.Avalonia.Views;

/// <summary>A dimmed layer with a centred "Pasting…" card and an indeterminate bar, shown over the result area during a long paste.</summary>
internal sealed class BusyOverlay : Panel
{
    private int _count;

    /// <summary>Creates the hidden overlay.</summary>
    public BusyOverlay()
    {
        IsVisible = false;
        AutomationProperties.SetName(this, UiText.Get("results.pasting"));
        var scrim = new Border { Opacity = 0.6 };
        scrim.Bind(Border.BackgroundProperty, scrim.GetResourceObservable("SquillBgBaseBrush"));
        var text = new TextBlock { Text = UiText.Get("results.pasting") };
        text.Bind(TextBlock.ForegroundProperty, text.GetResourceObservable("SquillFgBrush"));
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(text);
        stack.Children.Add(new ProgressBar { IsIndeterminate = true, Width = 220 });
        var card = new Border
        {
            Padding = new Thickness(16),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = stack,
        };
        card.Bind(Border.BackgroundProperty, card.GetResourceObservable("SquillBgOverlayBrush"));
        card.Bind(Border.BorderBrushProperty, card.GetResourceObservable("SquillBorderBrush"));
        Children.Add(scrim);
        Children.Add(card);
    }

    /// <summary>Shows the overlay; nested calls need as many <see cref="Hide"/> calls.</summary>
    public void Show()
    {
        _count++;
        IsVisible = true;
    }

    /// <summary>Hides the overlay once the last <see cref="Show"/> is matched.</summary>
    public void Hide()
    {
        if (_count > 0 && --_count == 0)
        {
            IsVisible = false;
        }
    }
}
