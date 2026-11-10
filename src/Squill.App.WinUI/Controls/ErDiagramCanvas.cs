using System.ComponentModel;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Squill.Presentation.Diagram;
using Squill.Presentation.Localization;
using Squill.Presentation.Results;
using Windows.System;
using Windows.UI;

namespace Squill.App.WinUI.Controls;

/// <summary>Custom-drawn ER diagram on Win2D: entity boxes and relation edges under the pan and zoom of a <see cref="DiagramTabViewModel"/>; it holds no layout logic.</summary>
public sealed partial class ErDiagramCanvas : Grid, IDisposable
{
    private const float ColumnFontSize = 12;
    private const float HeaderFontSize = 13;
    private const float MarkerFontSize = 9;
    private const float TagColumn = 26;
    private const float IconSize = 14;
    private const float DotPitch = 16;
    private const double HitTolerance = 5;
    private const float MinimapWidth = 209;
    private const float MinimapHeight = 108;
    private const float MinimapMargin = 12;
    private const float MinimapPadding = 8;
    private const float CrowLength = 10;
    private const float CrowSpread = 6;
    private const double ZoomStep = 1.1;
    private const double DragThreshold = 3;
    private const float Unbounded = 10_000;

    private static readonly string MonoFamily = ResolveMonoFamily();
    private static readonly ErLayoutOptions Metrics = new(MeasureText);

    private readonly CanvasControl _canvas = new();
    private readonly LruCache<string, CanvasTextLayout> _text = new(4096, layout => layout.Dispose());
    private CanvasRenderTarget? _dotTile;
    private CanvasImageBrush? _dotBrush;
    private DiagramTabViewModel? _diagram;
    private Palette? _palette;
    private ErPoint _pressed;
    private ErPoint _last;
    private bool _panning;
    private bool _dragging;
    private bool _moved;
    private bool _minimap;

    /// <summary>Creates the canvas.</summary>
    public ErDiagramCanvas()
    {
        Children.Add(_canvas);
        _canvas.Draw += OnDraw;
        _canvas.CreateResources += (_, _) =>
        {
            _text.Clear();
            ReleaseDots();
        };
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
            ReleaseDots();
            _canvas.Invalidate();
        };
        _canvas.Unloaded += (_, _) =>
        {
            _text.Clear();
            ReleaseDots();
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

    /// <summary>Gets or sets whether column data types are drawn.</summary>
    public bool ShowTypes
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            _canvas.Invalidate();
        }
    } = true;

    /// <summary>Gets the width of the canvas in device-independent pixels.</summary>
    public double CanvasWidth => _canvas.ActualWidth;

    /// <summary>Gets the height of the canvas in device-independent pixels.</summary>
    public double CanvasHeight => _canvas.ActualHeight;

    /// <summary>Measures a text in the diagram font so the shared layout matches the drawing.</summary>
    /// <param name="text">The text to measure.</param>
    /// <returns>Its width in device-independent pixels, plus the PK/FK tag column drawn before every name.</returns>
    public static double MeasureText(string text)
    {
        using var format = new CanvasTextFormat { FontSize = ColumnFontSize, FontFamily = MonoFamily, WordWrapping = CanvasWordWrapping.NoWrap };
        using var layout = new CanvasTextLayout(CanvasDevice.GetSharedDevice(), text, format, Unbounded, Unbounded);
        return layout.LayoutBounds.Width + TagColumn;
    }

    /// <summary>Fits the whole diagram into the current size of the canvas.</summary>
    public void FitToScreen() => _diagram?.FitToScreenCommand.Execute(new ErSize(_canvas.ActualWidth, _canvas.ActualHeight));

    private void OnDiagramChanged(object? sender, PropertyChangedEventArgs e) => _canvas.Invalidate();

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var ds = args.DrawingSession;
        var p = _palette ??= ResolvePalette();
        ds.Clear(p.Background);
        ds.FillRectangle(0, 0, (float)sender.ActualWidth, (float)sender.ActualHeight, DotBrush(sender, p));

        var d = _diagram;
        if (d is null)
            return;
        var topLeft = d.ToWorld(new ErPoint(0, 0));
        var bottomRight = d.ToWorld(new ErPoint(sender.ActualWidth, sender.ActualHeight));
        ds.Transform = Matrix3x2.CreateScale((float)d.Zoom) * Matrix3x2.CreateTranslation((float)d.OffsetX, (float)d.OffsetY);
        // Level of detail: a large schema zoomed out would otherwise draw thousands of text layouts per frame.
        var detail = d.Zoom >= 0.45 ? 2 : d.Zoom >= 0.2 ? 1 : 0;
        foreach (var edge in d.Edges)
        {
            if (Intersects(edge, topLeft, bottomRight))
                DrawEdge(ds, edge, IsHighlighted(d, edge) ? p.Accent : p.EdgeColor, detail > 0);
        }

        foreach (var entity in d.Model.Entities)
        {
            if (d.Boxes.TryGetValue(entity.Key, out var box) && box.X <= bottomRight.X && box.Right >= topLeft.X && box.Y <= bottomRight.Y && box.Bottom >= topLeft.Y)
                DrawEntity(ds, entity, box, string.Equals(entity.Key, d.SelectedKey, StringComparison.OrdinalIgnoreCase), p, detail);
        }

        ds.Transform = Matrix3x2.Identity;
        DrawMinimap(ds, d, sender, new Windows.Foundation.Rect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y), p);
    }

    /// <summary>Releases the cached dot-grid tile; the canvas also does it when it is unloaded.</summary>
    public void Dispose() => ReleaseDots();

    private void ReleaseDots()
    {
        _dotBrush?.Dispose();
        _dotTile?.Dispose();
        _dotBrush = null;
        _dotTile = null;
    }

    // The dot grid is one repeating 16 x 16 tile instead of a rectangle per dot.
    private CanvasImageBrush DotBrush(CanvasControl sender, Palette p)
    {
        if (_dotBrush is not null)
            return _dotBrush;
        _dotTile = new CanvasRenderTarget(sender, DotPitch, DotPitch, 96);
        using (var tile = _dotTile.CreateDrawingSession())
        {
            tile.Clear(Microsoft.UI.Colors.Transparent);
            tile.FillRectangle(8, 8, 1, 1, p.Dot);
        }

        return _dotBrush = new CanvasImageBrush(sender, _dotTile) { ExtendX = CanvasEdgeBehavior.Wrap, ExtendY = CanvasEdgeBehavior.Wrap };
    }

    private static bool Intersects(ErEdge edge, ErPoint topLeft, ErPoint bottomRight)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var point in edge.Points)
        {
            minX = Math.Min(minX, point.X);
            maxX = Math.Max(maxX, point.X);
            minY = Math.Min(minY, point.Y);
            maxY = Math.Max(maxY, point.Y);
        }

        return minX <= bottomRight.X && maxX >= topLeft.X && minY <= bottomRight.Y && maxY >= topLeft.Y;
    }

    private static bool IsHighlighted(DiagramTabViewModel d, ErEdge edge) =>
        d.SelectedKey is { } key
        && (string.Equals(edge.Relation.FromKey, key, StringComparison.OrdinalIgnoreCase)
            || string.Equals(edge.Relation.ToKey, key, StringComparison.OrdinalIgnoreCase));

    private void DrawEntity(CanvasDrawingSession ds, ErEntity entity, ErBox box, bool selected, Palette p, int detail)
    {
        float x = (float)box.X, y = (float)box.Y, w = (float)box.Width, h = (float)box.Height;
        var header = (float)Metrics.HeaderHeight;
        var row = (float)Metrics.RowHeight;
        var pad = (float)Metrics.Padding;
        ds.FillRoundedRectangle(x, y, w, h, p.Radius, p.Radius, p.Surface);
        // The header band: a rounded rectangle plus a square strip under its lower corners (no clipping layer, which is slow per entity).
        ds.FillRoundedRectangle(x, y, w, header, p.Radius, p.Radius, p.Header);
        ds.FillRectangle(x, y + header - p.Radius, w, p.Radius, p.Header);
        ds.DrawLine(x, y + header, x + w, y + header, p.Dot, 1);
        ds.DrawRoundedRectangle(x, y, w, h, p.Radius, p.Radius, selected ? p.Accent : p.Border, selected ? 1.5f : 1);
        if (detail == 0)
            return;
        DrawTableIcon(ds, x + pad, y + ((header - IconSize) / 2), p.Muted);
        var nameX = x + pad + TagColumn - 4;
        var title = Layout(ds, entity.Name, HeaderFontSize, bold: true);
        ds.DrawTextLayout(title, nameX, y + ((header - (float)title.LayoutBounds.Height) / 2), p.Text);
        if (detail < 2)
            return;
        for (var i = 0; i < entity.Columns.Count; i++)
        {
            var c = entity.Columns[i];
            var top = y + header + (row * i);
            var name = Layout(ds, c.Name, ColumnFontSize, bold: false);
            var ty = top + ((row - (float)name.LayoutBounds.Height) / 2);
            if (c.IsPrimaryKey || c.IsForeignKey)
            {
                var marker = Layout(ds, c.IsPrimaryKey ? "PK" : "FK", MarkerFontSize, bold: true);
                ds.DrawTextLayout(marker, x + pad - 2, top + ((row - (float)marker.LayoutBounds.Height) / 2), c.IsPrimaryKey ? p.Accent : p.Info);
            }

            ds.DrawTextLayout(name, nameX, ty, p.Text);
            if (!ShowTypes)
                continue;
            var type = Layout(ds, c.DataType, ColumnFontSize, bold: false);
            ds.DrawTextLayout(type, x + w - pad - (float)type.LayoutBounds.Width, ty, p.Type);
        }
    }

    // The table glyph of Icons.xaml (24 x 24 grid) scaled to IconSize.
    private static void DrawTableIcon(CanvasDrawingSession ds, float x, float y, Color color)
    {
        const float k = IconSize / 24;
        ds.DrawRectangle(x + (4 * k), y + (5 * k), 16 * k, 14 * k, color, 1.5f * k);
        ds.DrawLine(x + (4 * k), y + (10 * k), x + (20 * k), y + (10 * k), color, 1.5f * k);
        ds.DrawLine(x + (4 * k), y + (15 * k), x + (20 * k), y + (15 * k), color, 1.5f * k);
        ds.DrawLine(x + (10 * k), y + (5 * k), x + (10 * k), y + (19 * k), color, 1.5f * k);
    }

    private static void DrawMinimap(CanvasDrawingSession ds, DiagramTabViewModel d, CanvasControl canvas, Windows.Foundation.Rect view, Palette p)
    {
        if (d.Boxes.Count == 0 || canvas.ActualWidth < MinimapWidth + (2 * MinimapMargin) || canvas.ActualHeight < MinimapHeight + (2 * MinimapMargin))
            return;
        var panelX = (float)canvas.ActualWidth - MinimapWidth - MinimapMargin;
        var panelY = (float)canvas.ActualHeight - MinimapHeight - MinimapMargin;
        ds.FillRoundedRectangle(panelX, panelY, MinimapWidth, MinimapHeight, p.Radius, p.Radius, p.Header);
        ds.DrawRoundedRectangle(panelX, panelY, MinimapWidth, MinimapHeight, p.Radius, p.Radius, p.Border, 1);
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var box in d.Boxes.Values)
        {
            minX = Math.Min(minX, box.X);
            minY = Math.Min(minY, box.Y);
            maxX = Math.Max(maxX, box.Right);
            maxY = Math.Max(maxY, box.Bottom);
        }

        minX = Math.Min(minX, view.X);
        minY = Math.Min(minY, view.Y);
        maxX = Math.Max(maxX, view.Right);
        maxY = Math.Max(maxY, view.Bottom);
        var scale = Math.Min((MinimapWidth - (2 * MinimapPadding)) / Math.Max(1, maxX - minX), (MinimapHeight - (2 * MinimapPadding)) / Math.Max(1, maxY - minY));
        var offsetX = (MinimapWidth - (float)(Math.Max(1, maxX - minX) * scale)) / 2;
        var offsetY = (MinimapHeight - (float)(Math.Max(1, maxY - minY) * scale)) / 2;
        float Mx(double wx) => panelX + offsetX + (float)((wx - minX) * scale);
        float My(double wy) => panelY + offsetY + (float)((wy - minY) * scale);
        foreach (var box in d.Boxes.Values)
            ds.FillRectangle(Mx(box.X), My(box.Y), (float)(box.Width * scale), (float)(box.Height * scale), p.EdgeColor);
        ds.DrawRectangle(Mx(view.X), My(view.Y), (float)(view.Width * scale), (float)(view.Height * scale), p.Accent, 1.5f);
    }

    private static void DrawEdge(CanvasDrawingSession ds, ErEdge edge, Color color, bool markers)
    {
        if (edge.Points.Count < 2)
            return;
        for (var i = 1; i < edge.Points.Count; i++)
        {
            var (a, b) = (edge.Points[i - 1], edge.Points[i]);
            ds.DrawLine((float)a.X, (float)a.Y, (float)b.X, (float)b.Y, color, 1.5f);
        }

        if (!markers)
            return;
        DrawMarker(ds, edge.FromAnchor, edge.Points[0], edge.Points[1], edge.Relation.Cardinality == ErCardinality.ManyToOne, color);
        DrawMarker(ds, edge.ToAnchor, edge.Points[^1], edge.Points[^2], false, color);
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
                FontFamily = MonoFamily,
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
        if (d.CenterFromMinimap(screen, new ErSize(_canvas.ActualWidth, _canvas.ActualHeight)))
        {
            _minimap = true;
            _canvas.CapturePointer(e.Pointer);
            e.Handled = true;
            return;
        }

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
        if (d is null)
            return;
        var position = e.GetCurrentPoint(_canvas).Position;
        var screen = new ErPoint(position.X, position.Y);
        if (_minimap)
        {
            d.CenterFromMinimap(screen, new ErSize(_canvas.ActualWidth, _canvas.ActualHeight));
            return;
        }

        if (!_panning && !_dragging)
        {
            UpdateRelationTip(d, d.ToWorld(screen));
            return;
        }

        if (!_moved && Math.Abs(screen.X - _pressed.X) + Math.Abs(screen.Y - _pressed.Y) < DragThreshold)
            return;
        _moved = true;
        if (_panning)
            d.Pan(screen.X - _last.X, screen.Y - _last.Y);
        else
            d.DragTo(d.ToWorld(screen));
        _last = screen;
    }

    private void UpdateRelationTip(DiagramTabViewModel d, ErPoint world)
    {
        var tolerance = HitTolerance / d.Zoom;
        var hit = d.HitTestEntity(world) is null ? d.Edges.FirstOrDefault(edge => ErDiagramGeometry.Near(edge, world, tolerance)) : null;
        if (hit is null)
        {
            ToolTipService.SetToolTip(_canvas, null);
            return;
        }

        var r = hit.Relation;
        var from = r.FromKey + "." + string.Join(", ", r.FromColumns);
        var to = r.ToKey + "." + string.Join(", ", r.ToColumns);
        var mono = new Microsoft.UI.Xaml.Media.FontFamily(MonoFamily);
        var card = new StackPanel { Spacing = 2 };
        card.Children.Add(new TextBlock { Text = r.Name, FontFamily = mono, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        card.Children.Add(new TextBlock { Text = UiText.Format("er.relationFrom", from, to), FontFamily = mono, FontSize = ColumnFontSize });
        ToolTipService.SetToolTip(_canvas, card);
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_dragging && _moved)
            _diagram?.EndDrag();
        _dragging = false;
        _panning = false;
        _minimap = false;
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

    // Win2D takes one family: the first entry of the token's fallback list (the bundled file), resolved next to the executable.
    internal static string ResolveMonoFamily()
    {
        var family = ((Microsoft.UI.Xaml.Media.FontFamily)DataGridCanvas.FindMerged(Application.Current.Resources, "SquillFontMono")!).Source.Split(',')[0].Trim();
        const string AppX = "ms-appx:///";
        if (!family.StartsWith(AppX, StringComparison.Ordinal))
            return family;
        var parts = family[AppX.Length..].Split('#');
        return new Uri(Path.Combine(AppContext.BaseDirectory, parts[0])).AbsoluteUri + "#" + parts[1];
    }

    private Palette ResolvePalette()
    {
        var key = _canvas.ActualTheme == ElementTheme.Light ? "Light" : "Default";
        var theme = DataGridCanvas.FindThemeDictionary(Application.Current.Resources, key)!;
        Color Get(string name) => (Color)theme[name];
        var radius = DataGridCanvas.FindMerged(Application.Current.Resources, "SquillRadiusLg") is CornerRadius r ? (float)r.TopLeft : 8f;
        return new Palette(
            Get("SquillBgBaseColor"),
            Get("SquillBgRaisedColor"),
            Get("SquillBgSurfaceColor"),
            Get("SquillFgColor"),
            Get("SquillFg3Color"),
            Get("SquillAccentColor"),
            Get("SquillInfoColor"),
            Get("SquillSynTypeColor"),
            Get("SquillBorderSubtleColor"),
            Get("SquillBorderStrongColor"),
            Get("SquillBorderColor"),
            radius);
    }

    private sealed record Palette(Color Background, Color Surface, Color Header, Color Text, Color Muted, Color Accent, Color Info, Color Type, Color Dot, Color EdgeColor, Color Border, float Radius);
}
