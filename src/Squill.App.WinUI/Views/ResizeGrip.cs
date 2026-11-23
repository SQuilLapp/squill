using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using Squill.Presentation.Explorer;
using Squill.Presentation.Localization;

namespace Squill.App.WinUI.Views;

/// <summary>The bottom-right corner grip that resizes the explorer filter popup between its minimum and the window.</summary>
internal sealed partial class ResizeGrip : UserControl
{
    private readonly FrameworkElement _panel;
    private readonly Action<ExplorerFilterPopupSize> _released;
    private Windows.Foundation.Point _startPointer;
    private ExplorerFilterPopupSize _start;
    private double _maxWidth;
    private double _maxHeight;

    /// <summary>Creates the grip.</summary>
    /// <param name="panel">The element whose <c>Width</c> and <c>Height</c> the drag sets.</param>
    /// <param name="released">Called with the final size when the drag ends.</param>
    public ResizeGrip(FrameworkElement panel, Action<ExplorerFilterPopupSize> released)
    {
        _panel = panel;
        _released = released;
        Width = 14;
        Height = 14;
        HorizontalAlignment = HorizontalAlignment.Right;
        Margin = new Thickness(0, 0, 2, 2);
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthwestSoutheast);
        AutomationProperties.SetName(this, UiText.Get("explorer.filter.resize"));
        var near = new Line { X1 = 12, Y1 = 5, X2 = 5, Y2 = 12, StrokeThickness = 1.5 };
        var far = new Line { X1 = 12, Y1 = 9, X2 = 9, Y2 = 12, StrokeThickness = 1.5 };
        // The transparent canvas background makes the whole square hit-testable, not only the two lines.
        Content = new Canvas { Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent), Width = 14, Height = 14, Children = { near, far } };
        // The element's own theme, as Splitter does.
        void Paint()
        {
            var brush = Tokens.Brush(this, "SquillFg3Brush");
            near.Stroke = brush;
            far.Stroke = brush;
        }
        Loaded += (_, _) => Paint();
        ActualThemeChanged += (_, _) => Paint();
        PointerPressed += OnPressed;
        PointerMoved += OnMoved;
        PointerReleased += OnReleased;
        PointerCaptureLost += (_, _) => _released(new ExplorerFilterPopupSize(_panel.ActualWidth, _panel.ActualHeight));
    }

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        _startPointer = e.GetCurrentPoint(null).Position;
        _start = new ExplorerFilterPopupSize(_panel.ActualWidth, _panel.ActualHeight);
        var topLeft = _panel.TransformToVisual(null).TransformPoint(default);
        _maxWidth = XamlRoot.Size.Width - topLeft.X - ExplorerFilterPopupSize.WindowMargin;
        _maxHeight = XamlRoot.Size.Height - topLeft.Y - ExplorerFilterPopupSize.WindowMargin;
        CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        var p = e.GetCurrentPoint(null).Position;
        var size = ExplorerFilterPopupSize.Fit(new(_start.Width + p.X - _startPointer.X, _start.Height + p.Y - _startPointer.Y), _maxWidth, _maxHeight);
        _panel.Width = size.Width;
        _panel.Height = size.Height;
        e.Handled = true;
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }
}
