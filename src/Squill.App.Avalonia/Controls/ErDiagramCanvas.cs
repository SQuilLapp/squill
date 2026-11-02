using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Squill.Presentation.Diagram;

namespace Squill.App.Avalonia.Controls;

/// <summary>Custom-drawn ER diagram: entity boxes and relation edges under the pan and zoom of a <see cref="DiagramTabViewModel"/>; it holds no layout logic.</summary>
public sealed class ErDiagramCanvas : Control
{
    private const double ColumnFontSize = 12;
    private const double HeaderFontSize = 13;
    private const double MarkerFontSize = 9;
    private const double CrowLength = 10;
    private const double CrowSpread = 6;
    private const double ZoomStep = 1.1;
    private const double DragThreshold = 3;

    /// <summary>Identifies the <see cref="Diagram"/> property.</summary>
    public static readonly DirectProperty<ErDiagramCanvas, DiagramTabViewModel?> DiagramProperty =
        AvaloniaProperty.RegisterDirect<ErDiagramCanvas, DiagramTabViewModel?>(nameof(Diagram), o => o.Diagram, (o, v) => o.Diagram = v);

    private static readonly Typeface Face = new(FontFamily.Default);
    private static readonly Typeface BoldFace = new(FontFamily.Default, weight: FontWeight.SemiBold);
    private static readonly ErLayoutOptions Metrics = new(MeasureText);

    private DiagramTabViewModel? _diagram;
    private Palette? _palette;
    private ErPoint _pressed;
    private ErPoint _last;
    private bool _panning;
    private string? _draggedKey;
    private bool _moved;

    /// <summary>Creates the canvas.</summary>
    public ErDiagramCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        ActualThemeVariantChanged += (_, _) =>
        {
            _palette = null;
            InvalidateVisual();
        };
    }

    /// <summary>Gets or sets the diagram drawn and operated.</summary>
    public DiagramTabViewModel? Diagram
    {
        get => _diagram;
        set
        {
            var old = _diagram;
            if (!SetAndRaise(DiagramProperty, ref _diagram, value))
                return;
            if (old is not null)
                old.PropertyChanged -= OnDiagramChanged;
            if (value is not null)
                value.PropertyChanged += OnDiagramChanged;
            InvalidateVisual();
        }
    }

    /// <summary>Measures a text in the diagram font so the shared layout matches the drawing.</summary>
    /// <param name="text">The text to measure.</param>
    /// <returns>Its width in device-independent pixels.</returns>
    public static double MeasureText(string text) =>
        new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, ColumnFontSize, Brushes.Black).Width;

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var p = _palette ??= ResolvePalette();
        context.FillRectangle(p.Background, new Rect(Bounds.Size));
        var d = _diagram;
        if (d is null)
            return;
        var topLeft = d.ToWorld(new ErPoint(0, 0));
        var bottomRight = d.ToWorld(new ErPoint(Bounds.Width, Bounds.Height));
        var view = new Rect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
        using var _ = context.PushTransform(Matrix.CreateScale(d.Zoom, d.Zoom) * Matrix.CreateTranslation(d.OffsetX, d.OffsetY));
        foreach (var edge in d.Edges)
            DrawEdge(context, edge, p);
        foreach (var entity in d.Model.Entities)
        {
            if (d.Boxes.TryGetValue(entity.Key, out var box) && view.Intersects(new Rect(box.X, box.Y, box.Width, box.Height)))
                DrawEntity(context, entity, box, string.Equals(entity.Key, d.SelectedKey, StringComparison.OrdinalIgnoreCase), p);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var d = _diagram;
        if (d is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        Focus();
        var screen = ToPoint(e.GetPosition(this));
        _pressed = _last = screen;
        _moved = false;
        if (d.HitTestEntity(d.ToWorld(screen)) is { } hit)
        {
            _draggedKey = hit.Key;
            d.BeginDrag(hit.Key);
            if (e.ClickCount == 2)
                d.ActivateCommand.Execute(hit.Key);
        }
        else
        {
            _panning = true;
            d.SelectedKey = null;
        }

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var d = _diagram;
        if (d is null || (!_panning && _draggedKey is null))
            return;
        var screen = ToPoint(e.GetPosition(this));
        if (!_moved && Math.Abs(screen.X - _pressed.X) + Math.Abs(screen.Y - _pressed.Y) < DragThreshold)
            return;
        _moved = true;
        if (_panning)
            d.Pan(screen.X - _last.X, screen.Y - _last.Y);
        else
            d.DragTo(d.ToWorld(screen));
        _last = screen;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_draggedKey is not null && _moved)
            _diagram?.EndDrag();
        _draggedKey = null;
        _panning = false;
        e.Pointer.Capture(null);
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_diagram is null || e.Delta.Y == 0)
            return;
        _diagram.ZoomAt(ToPoint(e.GetPosition(this)), Math.Pow(ZoomStep, Math.Sign(e.Delta.Y)));
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.D0 or Key.NumPad0 && e.KeyModifiers == KeyModifiers.Control)
        {
            FitToScreen();
            e.Handled = true;
        }
    }

    /// <summary>Fits the whole diagram into the current size of the canvas.</summary>
    public void FitToScreen() => _diagram?.FitToScreenCommand.Execute(new ErSize(Bounds.Width, Bounds.Height));

    private static ErPoint ToPoint(Point p) => new(p.X, p.Y);

    private static Point ToPoint(ErPoint p) => new(p.X, p.Y);

    private static FormattedText Text(string text, double size, IBrush brush, Typeface face) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, brush);

    private void OnDiagramChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => InvalidateVisual();

    private static void DrawEntity(DrawingContext context, ErEntity entity, ErBox box, bool selected, Palette p)
    {
        var rect = new Rect(box.X, box.Y, box.Width, box.Height);
        var header = new Rect(box.X, box.Y, box.Width, Metrics.HeaderHeight);
        var border = selected ? p.Selected : p.Border;
        context.DrawRectangle(p.Surface, null, rect, p.Radius, p.Radius);
        context.DrawRectangle(p.Header, null, header, p.Radius, p.Radius);
        context.DrawRectangle(null, border, rect, p.Radius, p.Radius);
        var title = Text(entity.Name, HeaderFontSize, p.Text, BoldFace);
        context.DrawText(title, new Point(box.X + Metrics.Padding, box.Y + ((Metrics.HeaderHeight - title.Height) / 2)));
        for (var i = 0; i < entity.Columns.Count; i++)
        {
            var c = entity.Columns[i];
            var top = box.Y + Metrics.HeaderHeight + (Metrics.RowHeight * i);
            var name = Text(c.Name, ColumnFontSize, p.Text, Face);
            var y = top + ((Metrics.RowHeight - name.Height) / 2);
            context.DrawText(name, new Point(box.X + Metrics.Padding, y));
            var x = box.X + Metrics.Padding + name.Width;
            foreach (var (on, label, brush) in new[] { (c.IsPrimaryKey, "PK", p.Accent), (c.IsForeignKey, "FK", p.Muted) })
            {
                if (!on)
                    continue;
                var marker = Text(label, MarkerFontSize, brush, BoldFace);
                x += 4;
                context.DrawText(marker, new Point(x, top + ((Metrics.RowHeight - marker.Height) / 2)));
                x += marker.Width;
            }

            var type = Text(c.DataType, ColumnFontSize, p.Muted, Face);
            context.DrawText(type, new Point(box.Right - Metrics.Padding - type.Width, y));
        }
    }

    private static void DrawEdge(DrawingContext context, ErEdge edge, Palette p)
    {
        if (edge.Points.Count < 2)
            return;
        var pen = edge.Relation.IsOptional ? p.DashedEdge : p.Edge;
        for (var i = 1; i < edge.Points.Count; i++)
            context.DrawLine(pen, ToPoint(edge.Points[i - 1]), ToPoint(edge.Points[i]));
        DrawMarker(context, edge.FromAnchor, edge.Points[0], edge.Points[1], edge.Relation.Cardinality == ErCardinality.ManyToOne, p.Edge);
        DrawMarker(context, edge.ToAnchor, edge.Points[^1], edge.Points[^2], false, p.Edge);
    }

    // A crow's foot (many) or a bar (one) at an anchor, oriented along the first segment leaving it.
    private static void DrawMarker(DrawingContext context, ErPoint anchor, ErPoint from, ErPoint next, bool many, Pen pen)
    {
        double dx = next.X - from.X, dy = next.Y - from.Y;
        var len = Math.Sqrt((dx * dx) + (dy * dy));
        (dx, dy) = len < 1e-9 ? (1, 0) : (dx / len, dy / len);
        double px = -dy, py = dx;
        if (many)
        {
            var tip = new Point(anchor.X + (dx * CrowLength), anchor.Y + (dy * CrowLength));
            context.DrawLine(pen, tip, new Point(anchor.X + (px * CrowSpread), anchor.Y + (py * CrowSpread)));
            context.DrawLine(pen, tip, new Point(anchor.X - (px * CrowSpread), anchor.Y - (py * CrowSpread)));
        }
        else
        {
            double bx = anchor.X + (dx * 8), by = anchor.Y + (dy * 8);
            context.DrawLine(pen, new Point(bx + (px * CrowSpread), by + (py * CrowSpread)), new Point(bx - (px * CrowSpread), by - (py * CrowSpread)));
        }
    }

    private Palette ResolvePalette()
    {
        var accent = Brush("SquillAccentBrush");
        return new Palette(
            Brush("SquillBgBaseBrush"),
            Brush("SquillBgSurfaceBrush"),
            Brush("SquillBgRaisedBrush"),
            Brush("SquillFgBrush"),
            Brush("SquillFg3Brush"),
            accent,
            new Pen(Brush("SquillBorderBrush")),
            new Pen(accent, 2),
            new Pen(accent, 1.5),
            new Pen(accent, 1.5, new DashStyle([4, 3], 0)),
            Radius("SquillRadiusSm"));
    }

    private IBrush Brush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Gray;

    private double Radius(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is CornerRadius r ? r.TopLeft : 4;

    private sealed record Palette(
        IBrush Background,
        IBrush Surface,
        IBrush Header,
        IBrush Text,
        IBrush Muted,
        IBrush Accent,
        Pen Border,
        Pen Selected,
        Pen Edge,
        Pen DashedEdge,
        double Radius);
}
