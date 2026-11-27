using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Squill.Presentation.Explorer;
using Squill.Presentation.Localization;

namespace Squill.App.Avalonia.Views;

/// <summary>The bottom-right corner grip that resizes the explorer filter popup between its minimum and the main window.</summary>
internal sealed class ResizeGrip : Canvas
{
    private readonly Control _panel;
    private readonly Control _anchor;
    private readonly Action<ExplorerFilterPopupSize> _released;
    private bool _dragging;
    private PixelPoint _startPointer;
    private ExplorerFilterPopupSize _start;
    private double _maxWidth;
    private double _maxHeight;

    /// <summary>Creates the grip.</summary>
    /// <param name="panel">The element whose <c>Width</c> and <c>Height</c> the drag sets.</param>
    /// <param name="anchor">The control the popup hangs from; its window bounds the size.</param>
    /// <param name="released">Called with the final size when the drag ends.</param>
    public ResizeGrip(Control panel, Control anchor, Action<ExplorerFilterPopupSize> released)
    {
        _panel = panel;
        _anchor = anchor;
        _released = released;
        Name = "ResizeGrip";
        Width = 14;
        Height = 14;
        Background = Brushes.Transparent;
        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right;
        Margin = new Thickness(0, 0, 2, 2);
        Cursor = new Cursor(StandardCursorType.BottomRightCorner);
        AutomationProperties.SetName(this, UiText.Get("explorer.filter.resize"));
        foreach (var (y1, y2, x1, x2) in new[] { (5d, 12d, 12d, 5d), (9d, 12d, 12d, 9d) })
        {
            var line = new Line { StartPoint = new Point(x1, y1), EndPoint = new Point(x2, y2), StrokeThickness = 1.5 };
            line.Bind(Shape.StrokeProperty, this.GetResourceObservable("SquillFg3Brush"));
            Children.Add(line);
        }
    }

    /// <summary>Sizes the panel to its size when the drag began plus a delta, kept between the minimum and the main window.</summary>
    /// <param name="delta">The pointer travel in device-independent pixels.</param>
    internal void ResizeBy(Vector delta)
    {
        if (!_dragging)
            Begin(default);
        var size = ExplorerFilterPopupSize.Fit(new(_start.Width + delta.X, _start.Height + delta.Y), _maxWidth, _maxHeight);
        _panel.Width = size.Width;
        _panel.Height = size.Height;
    }

    /// <summary>Ends the drag and reports the final size.</summary>
    internal void EndResize()
    {
        _dragging = false;
        _released(new ExplorerFilterPopupSize(_panel.Width, _panel.Height));
    }

    // Limits in screen pixels, so they hold whether the popup is an overlay or its own window.
    private void Begin(PixelPoint pointer)
    {
        _dragging = true;
        _startPointer = pointer;
        _start = new ExplorerFilterPopupSize(double.IsNaN(_panel.Width) ? _panel.Bounds.Width : _panel.Width, double.IsNaN(_panel.Height) ? _panel.Bounds.Height : _panel.Height);
        if (TopLevel.GetTopLevel(_anchor) is { } main)
        {
            var bottomRight = main.PointToScreen(new Point(main.ClientSize.Width, main.ClientSize.Height));
            // Not in a visual tree (a popup that never opened): the anchor's corner stands in for the panel's.
            var topLeft = (TopLevel.GetTopLevel(_panel) is null ? _anchor : _panel).PointToScreen(default);
            var scale = main.RenderScaling;
            _maxWidth = ((bottomRight.X - topLeft.X) / scale) - ExplorerFilterPopupSize.WindowMargin;
            _maxHeight = ((bottomRight.Y - topLeft.Y) / scale) - ExplorerFilterPopupSize.WindowMargin;
        }
        else
        {
            _maxWidth = double.PositiveInfinity;
            _maxHeight = double.PositiveInfinity;
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Begin(this.PointToScreen(e.GetPosition(this)));
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging || !ReferenceEquals(e.Pointer.Captured, this))
            return;
        // Screen pixels: the popup's own origin can move while it grows (opening above).
        var now = this.PointToScreen(e.GetPosition(this));
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        ResizeBy(new Vector((now.X - _startPointer.X) / scale, (now.Y - _startPointer.Y) / scale));
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragging)
            EndResize();
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_dragging)
            EndResize();
    }
}
