using System.ComponentModel;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Squill.Presentation.Diagram;
using Squill.Presentation.Results;
using Windows.System;
using Windows.UI;

namespace Squill.App.WinUI.Controls;

/// <summary>Custom-drawn ER diagram on Win2D: entity boxes and relation edges under the pan and zoom of a <see cref="DiagramTabViewModel"/>; it holds no layout logic.</summary>
public sealed partial class ErDiagramCanvas : Grid
{
    private const float ColumnFontSize = 12;
    private const float HeaderFontSize = 13;
    private const float MarkerFontSize = 9;
    private const float CrowLength = 10;
    private const float CrowSpread = 6;
    private const double ZoomStep = 1.1;
    private const double DragThreshold = 3;
    private const float Unbounded = 10_000;

    private static readonly ErLayoutOptions Metrics = new(MeasureText);

    private readonly CanvasControl _canvas = new();
    private readonly LruCache<string, CanvasTextLayout> _text = new(1024, layout => layout.Dispose());
    private DiagramTabViewModel? _diagram;
    private Palette? _palette;
    private ErPoint _pressed;
    private ErPoint _last;
    private bool _panning;
    private bool _dragging;
    private bool _moved;

    /// <summary>Creates the canvas.</summary>
    public ErDiagramCanvas()
    {
        Children.Add(_canvas);
        _canvas.Draw += OnDraw;
        _canvas.CreateResources += (_, _) => _text.Clear();
        _canvas.IsTabStop = true;
        _canvas.PointerPressed += OnPointerPressed;
        _canvas.PointerMoved += OnPointerMoved;
        _canvas.PointerReleased += OnPointerReleased;
        _canvas.PointerCanceled += OnPointerReleased;
        _canvas.PointerWheelChanged += OnPointerWheelChanged;
        _canvas.DoubleTapped += OnDoubleTapped;
        _canvas.KeyDown += OnKeyDown;
        _canvas.ActualThemeChanged += (_, _) =>
        {
            _palette = null;
            _canvas.Invalidate();
        };
        _canvas.Unloaded += (_, _) =>
        {
            _text.Clear();
            _canvas.RemoveFromVisualTree();
        };
    }

    /// <summary>Gets or sets the diagram drawn and operated.</summary>
    public DiagramTabViewModel? Diagram
    {
        get => _diagram;
        set
        {
            if (_diagram is not null)
                _diagram.PropertyChanged -= OnDiagramChanged;
            _diagram = value;
            if (_diagram is not null)
                _diagram.PropertyChanged += OnDiagramChanged;
            _canvas.Invalidate();
        }
    }

    /// <summary>Measures a text in the diagram font so the shared layout matches the drawing.</summary>
    /// <param name="text">The text to measure.</param>
    /// <returns>Its width in device-independent pixels.</returns>
    public static double MeasureText(string text)
    {
        using var format = new CanvasTextFormat { FontSize = ColumnFontSize, WordWrapping = CanvasWordWrapping.NoWrap };
        using var layout = new CanvasTextLayout(CanvasDevice.GetSharedDevice(), text, format, Unbounded, Unbounded);
        return layout.LayoutBounds.Width;
    }

    /// <summary>Fits the whole diagram into the current size of the canvas.</summary>
    public void FitToScreen() => _diagram?.FitToScreenCommand.Execute(new ErSize(_canvas.ActualWidth, _canvas.ActualHeight));

    private void OnDiagramChanged(object? sender, PropertyChangedEventArgs e) => _canvas.Invalidate();

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var ds = args.DrawingSession;
        var p = _palette ??= ResolvePalette();
        ds.Clear(p.Background);
        var d = _diagram;
        if (d is null)
            return;
        var topLeft = d.ToWorld(new ErPoint(0, 0));
        var bottomRight = d.ToWorld(new ErPoint(sender.ActualWidth, sender.ActualHeight));
        ds.Transform = Matrix3x2.CreateScale((float)d.Zoom) * Matrix3x2.CreateTranslation((float)d.OffsetX, (float)d.OffsetY);
        using var dashed = new CanvasStrokeStyle { CustomDashStyle = [4, 3] };
        foreach (var edge in d.Edges)
            DrawEdge(ds, edge, p, dashed);
        foreach (var entity in d.Model.Entities)
        {
            if (d.Boxes.TryGetValue(entity.Key, out var box) && box.X <= bottomRight.X && box.Right >= topLeft.X && box.Y <= bottomRight.Y && box.Bottom >= topLeft.Y)
                DrawEntity(ds, entity, box, string.Equals(entity.Key, d.SelectedKey, StringComparison.OrdinalIgnoreCase), p);
        }
    }

    private void DrawEntity(CanvasDrawingSession ds, ErEntity entity, ErBox box, bool selected, Palette p)
    {
        float x = (float)box.X, y = (float)box.Y, w = (float)box.Width, h = (float)box.Height;
        var header = (float)Metrics.HeaderHeight;
        var row = (float)Metrics.RowHeight;
        var pad = (float)Metrics.Padding;
        ds.FillRoundedRectangle(x, y, w, h, p.Radius, p.Radius, p.Surface);
        using (ds.CreateLayer(1f, new Windows.Foundation.Rect(x, y, w, header)))
            ds.FillRoundedRectangle(x, y, w, h, p.Radius, p.Radius, p.Header);
        ds.DrawRoundedRectangle(x, y, w, h, p.Radius, p.Radius, selected ? p.Accent : p.Border, selected ? 2 : 1);
        var title = Layout(ds, entity.Name, HeaderFontSize, bold: true);
        ds.DrawTextLayout(title, x + pad, y + ((header - (float)title.LayoutBounds.Height) / 2), p.Text);
        for (var i = 0; i < entity.Columns.Count; i++)
        {
            var c = entity.Columns[i];
            var top = y + header + (row * i);
            var name = Layout(ds, c.Name, ColumnFontSize, bold: false);
            var ty = top + ((row - (float)name.LayoutBounds.Height) / 2);
            ds.DrawTextLayout(name, x + pad, ty, p.Text);
            var mx = x + pad + (float)name.LayoutBounds.Width;
            foreach (var (on, label, color) in new[] { (c.IsPrimaryKey, "PK", p.Accent), (c.IsForeignKey, "FK", p.Muted) })
            {
                if (!on)
                    continue;
                var marker = Layout(ds, label, MarkerFontSize, bold: true);
                mx += 4;
                ds.DrawTextLayout(marker, mx, top + ((row - (float)marker.LayoutBounds.Height) / 2), color);
                mx += (float)marker.LayoutBounds.Width;
            }

            var type = Layout(ds, c.DataType, ColumnFontSize, bold: false);
            ds.DrawTextLayout(type, x + w - pad - (float)type.LayoutBounds.Width, ty, p.Muted);
        }
    }

    private static void DrawEdge(CanvasDrawingSession ds, ErEdge edge, Palette p, CanvasStrokeStyle dashed)
    {
        if (edge.Points.Count < 2)
            return;
        for (var i = 1; i < edge.Points.Count; i++)
        {
            var (a, b) = (edge.Points[i - 1], edge.Points[i]);
            if (edge.Relation.IsOptional)
                ds.DrawLine((float)a.X, (float)a.Y, (float)b.X, (float)b.Y, p.Accent, 1.5f, dashed);
            else
                ds.DrawLine((float)a.X, (float)a.Y, (float)b.X, (float)b.Y, p.Accent, 1.5f);
        }

        DrawMarker(ds, edge.FromAnchor, edge.Points[0], edge.Points[1], edge.Relation.Cardinality == ErCardinality.ManyToOne, p.Accent);
        DrawMarker(ds, edge.ToAnchor, edge.Points[^1], edge.Points[^2], false, p.Accent);
    }

    // A crow's foot (many) or a bar (one) at an anchor, oriented along the first segment leaving it.
    private static void DrawMarker(CanvasDrawingSession ds, ErPoint anchor, ErPoint from, ErPoint next, bool many, Color color)
    {
        double dx = next.X - from.X, dy = next.Y - from.Y;
        var len = Math.Sqrt((dx * dx) + (dy * dy));
        (dx, dy) = len < 1e-9 ? (1, 0) : (dx / len, dy / len);
        double px = -dy, py = dx;
        float ax = (float)anchor.X, ay = (float)anchor.Y;
        if (many)
        {
            float tx = ax + ((float)dx * CrowLength), ty = ay + ((float)dy * CrowLength);
            ds.DrawLine(tx, ty, ax + ((float)px * CrowSpread), ay + ((float)py * CrowSpread), color, 1.5f);
            ds.DrawLine(tx, ty, ax - ((float)px * CrowSpread), ay - ((float)py * CrowSpread), color, 1.5f);
        }
        else
        {
            float bx = ax + ((float)dx * 8), by = ay + ((float)dy * 8);
            ds.DrawLine(bx + ((float)px * CrowSpread), by + ((float)py * CrowSpread), bx - ((float)px * CrowSpread), by - ((float)py * CrowSpread), color, 1.5f);
        }
    }

    private CanvasTextLayout Layout(ICanvasResourceCreator creator, string text, float size, bool bold)
    {
        var key = $"{size}{(bold ? 'b' : 'n')}{text}";
        if (!_text.TryGet(key, out var layout))
        {
            // The layout copies the format, so a short-lived one is enough.
            using var format = new CanvasTextFormat
            {
                FontSize = size,
                FontWeight = bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                WordWrapping = CanvasWordWrapping.NoWrap,
            };
            layout = new CanvasTextLayout(creator, text, format, Unbounded, Unbounded);
            _text.Add(key, layout);
        }

        return layout;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var d = _diagram;
        var point = e.GetCurrentPoint(_canvas);
        if (d is null || !point.Properties.IsLeftButtonPressed)
            return;
        _canvas.Focus(FocusState.Pointer);
        var screen = new ErPoint(point.Position.X, point.Position.Y);
        _pressed = _last = screen;
        _moved = false;
        if (d.HitTestEntity(d.ToWorld(screen)) is { } hit)
        {
            _dragging = true;
            d.BeginDrag(hit.Key);
        }
        else
        {
            _panning = true;
            d.SelectedKey = null;
        }

        _canvas.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var d = _diagram;
        if (d is null || (!_panning && !_dragging))
            return;
        var position = e.GetCurrentPoint(_canvas).Position;
        var screen = new ErPoint(position.X, position.Y);
        if (!_moved && Math.Abs(screen.X - _pressed.X) + Math.Abs(screen.Y - _pressed.Y) < DragThreshold)
            return;
        _moved = true;
        if (_panning)
            d.Pan(screen.X - _last.X, screen.Y - _last.Y);
        else
            d.DragTo(d.ToWorld(screen));
        _last = screen;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_dragging && _moved)
            _diagram?.EndDrag();
        _dragging = false;
        _panning = false;
        _canvas.ReleasePointerCapture(e.Pointer);
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_canvas);
        if (_diagram is null || point.Properties.MouseWheelDelta == 0)
            return;
        _diagram.ZoomAt(new ErPoint(point.Position.X, point.Position.Y), Math.Pow(ZoomStep, Math.Sign(point.Properties.MouseWheelDelta)));
        e.Handled = true;
    }

    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        var d = _diagram;
        if (d is null)
            return;
        var position = e.GetPosition(_canvas);
        if (d.HitTestEntity(d.ToWorld(new ErPoint(position.X, position.Y))) is { } hit)
            d.ActivateCommand.Execute(hit.Key);
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (ctrl && e.Key is VirtualKey.Number0 or VirtualKey.NumberPad0)
        {
            FitToScreen();
            e.Handled = true;
        }
    }

    private Palette ResolvePalette()
    {
        var key = _canvas.ActualTheme == ElementTheme.Light ? "Light" : "Default";
        var theme = DataGridCanvas.FindThemeDictionary(Application.Current.Resources, key)!;
        Color Get(string name) => (Color)theme[name];
        var radius = DataGridCanvas.FindMerged(Application.Current.Resources, "SquillRadiusSm") is CornerRadius r ? (float)r.TopLeft : 4f;
        return new Palette(
            Get("SquillBgBaseColor"),
            Get("SquillBgSurfaceColor"),
            Get("SquillBgRaisedColor"),
            Get("SquillFgColor"),
            Get("SquillFg3Color"),
            Get("SquillAccentColor"),
            Get("SquillBorderColor"),
            radius);
    }

    private sealed record Palette(Color Background, Color Surface, Color Header, Color Text, Color Muted, Color Accent, Color Border, float Radius);
}
