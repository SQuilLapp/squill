using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Squill.Presentation.Diagram;
using Squill.Presentation.Localization;

namespace Squill.App.Avalonia.Controls;

/// <summary>Custom-drawn ER diagram: entity boxes and relation edges under the pan and zoom of a <see cref="DiagramTabViewModel"/>; it holds no layout logic.</summary>
public sealed class ErDiagramCanvas : Control
{
    private const double ColumnFontSize = 12;
    private const double HeaderFontSize = 13;
    private const double MarkerFontSize = 9;
    private const double TagColumn = 26;
    private const double IconSize = 14;
    private const double DotPitch = 16;
    private const double HitTolerance = 5;
    private const double MinimapWidth = 209;
    private const double MinimapHeight = 108;
    private const double MinimapMargin = 12;
    private const double MinimapPadding = 8;
    private const double CrowLength = 10;
    private const double CrowSpread = 6;
    private const double ZoomStep = 1.1;
    private const double DragThreshold = 3;

    /// <summary>Identifies the <see cref="Diagram"/> property.</summary>
    public static readonly DirectProperty<ErDiagramCanvas, DiagramTabViewModel?> DiagramProperty =
        AvaloniaProperty.RegisterDirect<ErDiagramCanvas, DiagramTabViewModel?>(nameof(Diagram), o => o.Diagram, (o, v) => o.Diagram = v);

    internal static readonly FontFamily Mono = new("avares://Squill.App.Avalonia/Assets/Fonts#JetBrains Mono, Cascadia Code, monospace");
    private static readonly Typeface Face = new(Mono);
    private static readonly Typeface BoldFace = new(Mono, weight: FontWeight.SemiBold);
    private static readonly ErLayoutOptions Metrics = new(MeasureText);

    private DiagramTabViewModel? _diagram;
    private Palette? _palette;
    private ErPoint _pressed;
    private ErPoint _last;
    private bool _panning;
    private bool _minimap;
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

    /// <summary>Gets or sets whether column data types are drawn.</summary>
    public bool ShowTypes
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            InvalidateVisual();
        }
    } = true;

    /// <summary>Measures a text in the diagram font so the shared layout matches the drawing.</summary>
    /// <param name="text">The text to measure.</param>
    /// <returns>Its width in device-independent pixels, plus the PK/FK tag column drawn before every name.</returns>
    public static double MeasureText(string text) =>
        new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, ColumnFontSize, Brushes.Black).Width + TagColumn;

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var p = _palette ??= ResolvePalette();
        context.FillRectangle(p.Background, new Rect(Bounds.Size));
        for (var x = 8.0; x < Bounds.Width; x += DotPitch)
        {
            for (var y = 8.0; y < Bounds.Height; y += DotPitch)
                context.FillRectangle(p.Dot, new Rect(x, y, 1, 1));
        }

        var d = _diagram;
        if (d is null)
            return;
        var topLeft = d.ToWorld(new ErPoint(0, 0));
        var bottomRight = d.ToWorld(new ErPoint(Bounds.Width, Bounds.Height));
        var view = new Rect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
        using (context.PushTransform(Matrix.CreateScale(d.Zoom, d.Zoom) * Matrix.CreateTranslation(d.OffsetX, d.OffsetY)))
        {
            foreach (var edge in d.Edges)
                DrawEdge(context, edge, IsHighlighted(d, edge), p);
            foreach (var entity in d.Model.Entities)
            {
                if (d.Boxes.TryGetValue(entity.Key, out var box) && view.Intersects(new Rect(box.X, box.Y, box.Width, box.Height)))
                    DrawEntity(context, entity, box, string.Equals(entity.Key, d.SelectedKey, StringComparison.OrdinalIgnoreCase), p);
            }
        }

        DrawMinimap(context, d, view, p);
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
        if (d.CenterFromMinimap(screen, new ErSize(Bounds.Width, Bounds.Height)))
        {
            _minimap = true;
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

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
        if (d is null)
            return;
        var screen = ToPoint(e.GetPosition(this));
        if (_minimap)
        {
            d.CenterFromMinimap(screen, new ErSize(Bounds.Width, Bounds.Height));
            return;
        }

        if (!_panning && _draggedKey is null)
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

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_draggedKey is not null && _moved)
            _diagram?.EndDrag();
        _draggedKey = null;
        _panning = false;
        _minimap = false;
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

    private static bool IsHighlighted(DiagramTabViewModel d, ErEdge edge) =>
        d.SelectedKey is { } key
        && (string.Equals(edge.Relation.FromKey, key, StringComparison.OrdinalIgnoreCase)
            || string.Equals(edge.Relation.ToKey, key, StringComparison.OrdinalIgnoreCase));

    private void UpdateRelationTip(DiagramTabViewModel d, ErPoint world)
    {
        var tolerance = HitTolerance / d.Zoom;
        var hit = d.HitTestEntity(world) is null ? d.Edges.FirstOrDefault(edge => Near(edge, world, tolerance)) : null;
        if (hit is null)
        {
            ToolTip.SetTip(this, null);
            return;
        }

        var r = hit.Relation;
        var from = r.FromKey + "." + string.Join(", ", r.FromColumns);
        var to = r.ToKey + "." + string.Join(", ", r.ToColumns);
        var card = new StackPanel { Spacing = 2 };
        card.Children.Add(new TextBlock { Text = r.Name, FontFamily = Mono, FontWeight = FontWeight.SemiBold });
        card.Children.Add(new TextBlock { Text = UiText.Format("er.relationFrom", from, to), FontFamily = Mono, FontSize = ColumnFontSize });
        ToolTip.SetTip(this, card);
    }

    private static bool Near(ErEdge edge, ErPoint p, double tolerance)
    {
        for (var i = 1; i < edge.Points.Count; i++)
        {
            double ax = edge.Points[i - 1].X, ay = edge.Points[i - 1].Y;
            double dx = edge.Points[i].X - ax, dy = edge.Points[i].Y - ay;
            var len2 = (dx * dx) + (dy * dy);
            var t = len2 < 1e-9 ? 0 : Math.Clamp((((p.X - ax) * dx) + ((p.Y - ay) * dy)) / len2, 0, 1);
            double ex = p.X - (ax + (t * dx)), ey = p.Y - (ay + (t * dy));
            if ((ex * ex) + (ey * ey) <= tolerance * tolerance)
                return true;
        }

        return false;
    }

    private void DrawEntity(DrawingContext context, ErEntity entity, ErBox box, bool selected, Palette p)
    {
        var rect = new Rect(box.X, box.Y, box.Width, box.Height);
        var header = new Rect(box.X, box.Y, box.Width, Metrics.HeaderHeight);
        context.DrawRectangle(p.Surface, null, rect, p.Radius, p.Radius);
        context.DrawRectangle(p.Header, null, new RoundedRect(header, new CornerRadius(p.Radius, p.Radius, 0, 0)));
        context.DrawLine(p.Divider, new Point(box.X, header.Bottom), new Point(box.Right, header.Bottom));
        context.DrawRectangle(null, selected ? p.Selected : p.Border, rect, p.Radius, p.Radius);
        if (p.TableIcon is { } icon)
        {
            var move = Matrix.CreateScale(IconSize / 24, IconSize / 24) * Matrix.CreateTranslation(box.X + Metrics.Padding, box.Y + ((Metrics.HeaderHeight - IconSize) / 2));
            using (context.PushTransform(move))
                context.DrawGeometry(null, p.IconPen, icon);
        }

        var nameX = box.X + Metrics.Padding + TagColumn - 4;
        var title = Text(entity.Name, HeaderFontSize, p.Text, BoldFace);
        context.DrawText(title, new Point(nameX, box.Y + ((Metrics.HeaderHeight - title.Height) / 2)));
        for (var i = 0; i < entity.Columns.Count; i++)
        {
            var c = entity.Columns[i];
            var top = box.Y + Metrics.HeaderHeight + (Metrics.RowHeight * i);
            var name = Text(c.Name, ColumnFontSize, p.Text, Face);
            var y = top + ((Metrics.RowHeight - name.Height) / 2);
            if (c.IsPrimaryKey || c.IsForeignKey)
            {
                var marker = Text(c.IsPrimaryKey ? "PK" : "FK", MarkerFontSize, c.IsPrimaryKey ? p.Accent : p.Info, BoldFace);
                context.DrawText(marker, new Point(box.X + Metrics.Padding - 2, top + ((Metrics.RowHeight - marker.Height) / 2)));
            }

            context.DrawText(name, new Point(nameX, y));
            if (!ShowTypes)
                continue;
            var type = Text(c.DataType, ColumnFontSize, p.Type, Face);
            context.DrawText(type, new Point(box.Right - Metrics.Padding - type.Width, y));
        }
    }

    private void DrawMinimap(DrawingContext context, DiagramTabViewModel d, Rect view, Palette p)
    {
        if (d.Boxes.Count == 0 || Bounds.Width < MinimapWidth + (2 * MinimapMargin) || Bounds.Height < MinimapHeight + (2 * MinimapMargin))
            return;
        var panel = new Rect(Bounds.Width - MinimapWidth - MinimapMargin, Bounds.Height - MinimapHeight - MinimapMargin, MinimapWidth, MinimapHeight);
        context.DrawRectangle(p.Header, p.Border, panel, p.Radius, p.Radius);
        var world = view;
        foreach (var box in d.Boxes.Values)
            world = world.Union(new Rect(box.X, box.Y, box.Width, box.Height));
        var scale = Math.Min((panel.Width - (2 * MinimapPadding)) / world.Width, (panel.Height - (2 * MinimapPadding)) / world.Height);
        var origin = new Point(
            panel.X + ((panel.Width - (world.Width * scale)) / 2) - (world.X * scale),
            panel.Y + ((panel.Height - (world.Height * scale)) / 2) - (world.Y * scale));
        Rect Map(Rect r) => new(origin.X + (r.X * scale), origin.Y + (r.Y * scale), r.Width * scale, r.Height * scale);
        using (context.PushClip(panel))
        {
            foreach (var box in d.Boxes.Values)
                context.FillRectangle(p.MinimapNode, Map(new Rect(box.X, box.Y, box.Width, box.Height)));
            context.DrawRectangle(p.AccentSoft, new Pen(p.Accent, 1), Map(view));
        }
    }

    private static void DrawEdge(DrawingContext context, ErEdge edge, bool highlighted, Palette p)
    {
        if (edge.Points.Count < 2)
            return;
        var solid = highlighted ? p.EdgeHighlight : p.Edge;
        var pen = edge.Relation.IsOptional ? new Pen(solid.Brush, solid.Thickness, new DashStyle([4, 3], 0)) : solid;
        for (var i = 1; i < edge.Points.Count; i++)
            context.DrawLine(pen, ToPoint(edge.Points[i - 1]), ToPoint(edge.Points[i]));
        DrawMarker(context, edge.FromAnchor, edge.Points[0], edge.Points[1], edge.Relation.Cardinality == ErCardinality.ManyToOne, solid);
        DrawMarker(context, edge.ToAnchor, edge.Points[^1], edge.Points[^2], false, solid);
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
        var strong = Brush("SquillBorderStrongBrush");
        var muted = Brush("SquillFg3Brush");
        return new Palette(
            Brush("SquillBgBaseBrush"),
            Brush("SquillBgRaisedBrush"),
            Brush("SquillBgSurfaceBrush"),
            Brush("SquillFgBrush"),
            accent,
            Brush("SquillInfoBrush"),
            Brush("SquillSynTypeBrush"),
            Brush("SquillAccentSoftBrush"),
            Brush("SquillBorderSubtleBrush"),
            strong,
            new Pen(Brush("SquillBorderBrush")),
            new Pen(Brush("SquillBorderSubtleBrush")),
            new Pen(accent, 1.5),
            new Pen(strong, 1.5),
            new Pen(accent, 1.5),
            new Pen(muted, 1.5),
            this.TryFindResource("SquillIconTable", ActualThemeVariant, out var icon) ? icon as Geometry : null,
            Radius("SquillRadiusLg"));
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
        IBrush Accent,
        IBrush Info,
        IBrush Type,
        IBrush AccentSoft,
        IBrush Dot,
        IBrush MinimapNode,
        Pen Border,
        Pen Divider,
        Pen Selected,
        Pen Edge,
        Pen EdgeHighlight,
        Pen IconPen,
        Geometry? TableIcon,
        double Radius);
}
