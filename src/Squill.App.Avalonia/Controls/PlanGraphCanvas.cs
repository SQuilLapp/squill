using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Squill.Core.Plans;
using Squill.Presentation.Localization;
using Squill.Presentation.Plans;

namespace Squill.App.Avalonia.Controls;

/// <summary>Custom-drawn query plan graph: node cards, pills and edges of a <see cref="PlanLayoutResult"/> under the zoom of a <see cref="PlanViewModel"/> and a pan the canvas keeps itself.</summary>
public sealed class PlanGraphCanvas : Control
{
    private const double DotPitch = 16;
    private const double Pad = 12;
    private const double ZoomStep = 1.1;
    private const double DragThreshold = 3;
    private const double MonoSize = 12;
    private const double WheelPan = 48;
    private const int MaxCachedTexts = 4096;

    private static readonly FontFamily Mono = ErDiagramCanvas.Mono;
    private static readonly Typeface Face = new(Mono);
    private static readonly Typeface BoldFace = new(Typeface.Default.FontFamily, weight: FontWeight.SemiBold);

    private readonly Dictionary<(string Text, double Size, bool Mono, bool Bold, int Width, int Brush), FormattedText> _text = [];
    private readonly Dictionary<PlanEdgePath, StreamGeometry> _edges = [];
    private PlanViewModel? _plan;
    private PlanLayoutResult? _layout;
    private Palette? _palette;
    private double _offsetX;
    private double _offsetY;
    private Point _pressed;
    private Point _last;
    private bool _panning;
    private bool _moved;

    /// <summary>Creates the canvas.</summary>
    public PlanGraphCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        // DoubleTapped fits the plan to the width, like the WinUI canvas.
        DoubleTapped += (_, _) => FitWidth();
        ActualThemeVariantChanged += (_, _) =>
        {
            _palette = null;
            _text.Clear();
            InvalidateVisual();
        };
    }

    /// <summary>Gets or sets the plan drawn; a new plan recomputes the layout and resets the pan.</summary>
    public PlanViewModel? Plan
    {
        get => _plan;
        set
        {
            if (ReferenceEquals(_plan, value))
                return;
            if (_plan is not null)
                _plan.PropertyChanged -= OnPlanChanged;
            _plan = value;
            _text.Clear();
            _edges.Clear();
            _offsetX = 0;
            _offsetY = 0;
            _layout = null;
            if (_plan is not null)
            {
                _plan.PropertyChanged += OnPlanChanged;
                _layout = PlanLayout.Compute(_plan.Rows, MeasureText);
            }

            InvalidateVisual();
        }
    }

    /// <summary>Zooms to fit the width of the plan into the canvas, never beyond 100 %.</summary>
    public void FitWidth()
    {
        if (_plan is null || _layout is not { Width: > 0 } layout || Bounds.Width <= 0)
            return;
        _plan.Zoom = Math.Min(1, Bounds.Width / layout.Width);
        _offsetX = 0;
        _offsetY = 0;
        InvalidateVisual();
    }

    private static double MeasureText(string text) =>
        new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, MonoSize, Brushes.Black).Width;

    private void OnPlanChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlanViewModel.SelectedRow))
            RevealSelected();
        InvalidateVisual();
    }

    // The table (or a click) selected a node: pan so that its card is visible.
    private void RevealSelected()
    {
        if (_plan is null || _layout is null || Bounds.Width <= 0)
            return;
        var zoom = _plan.Zoom;
        foreach (var box in _layout.Nodes)
        {
            if (!ReferenceEquals(_plan.Rows[box.Row], _plan.SelectedRow))
                continue;
            var left = (box.X * zoom) + _offsetX;
            var top = (box.Y * zoom) + _offsetY;
            if (left < 0 || left + (box.Width * zoom) > Bounds.Width)
                _offsetX = (Bounds.Width / 2) - ((box.X + (box.Width / 2)) * zoom);
            if (top < 0 || top + (box.Height * zoom) > Bounds.Height)
                _offsetY = (Bounds.Height / 2) - ((box.Y + (box.Height / 2)) * zoom);
            return;
        }
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var p = _palette ??= ResolvePalette();
        context.FillRectangle(p.BackgroundBrush, new Rect(Bounds.Size));
        for (var x = DotPitch / 2; x < Bounds.Width; x += DotPitch)
        {
            for (var y = DotPitch / 2; y < Bounds.Height; y += DotPitch)
                context.FillRectangle(p.BorderSubtleBrush, new Rect(x, y, 1, 1));
        }

        if (_plan is not { } plan || _layout is not { } layout)
            return;

        var zoom = plan.Zoom;
        // Only what intersects the viewport is drawn, so plans with hundreds of nodes stay smooth.
        var view = new Rect(-_offsetX / zoom, -_offsetY / zoom, Bounds.Width / zoom, Bounds.Height / zoom);
        using var transform = context.PushTransform(Matrix.CreateScale(zoom, zoom) * Matrix.CreateTranslation(_offsetX, _offsetY));

        foreach (var edge in layout.Edges)
        {
            var minX = Math.Min(edge.X0, edge.X1);
            var maxX = Math.Max(edge.X0, edge.X1);
            var minY = Math.Min(Math.Min(edge.Y0, edge.Y1), Math.Min(edge.C1Y, edge.C2Y));
            var maxY = Math.Max(Math.Max(edge.Y0, edge.Y1), Math.Max(edge.C1Y, edge.C2Y));
            if (maxX < view.Left || minX > view.Right || maxY < view.Top || minY > view.Bottom)
                continue;
            if (!_edges.TryGetValue(edge, out var geometry))
            {
                geometry = new StreamGeometry();
                using (var g = geometry.Open())
                {
                    g.BeginFigure(new Point(edge.X0, edge.Y0), false);
                    g.CubicBezierTo(new Point(edge.C1X, edge.C1Y), new Point(edge.C2X, edge.C2Y), new Point(edge.X1, edge.Y1));
                    g.EndFigure(false);
                }

                _edges[edge] = geometry;
            }

            var color = edge.Severity switch { PlanSeverity.Critical => p.Danger, PlanSeverity.Alert => p.Warning, _ => p.EdgeStrong };
            var brush = new SolidColorBrush(color, 0.55);
            context.DrawGeometry(null, new Pen(brush, edge.Thickness, lineCap: PenLineCap.Round), geometry);
        }

        foreach (var pill in layout.Pills)
        {
            if (pill.X + pill.Width < view.Left || pill.X > view.Right || pill.Y + pill.Height < view.Top || pill.Y > view.Bottom)
                continue;
            var rect = new Rect(pill.X, pill.Y, pill.Width, pill.Height);
            context.DrawRectangle(p.BackgroundBrush, new Pen(p.EdgeStrongBrush, 1, DashStyle.Dash), rect, 11, 11);
            context.DrawText(Layout(pill.Text, MonoSize, true, false, pill.Width - 8, p.Fg2Brush), new Point(pill.X + 4, pill.Y + ((pill.Height - 16) / 2)));
        }

        foreach (var box in layout.Nodes)
        {
            if (box.X + box.Width < view.Left || box.X > view.Right || box.Y + box.Height < view.Top || box.Y > view.Bottom)
                continue;
            DrawCard(context, plan, box, p);
        }
    }

    private void DrawCard(DrawingContext context, PlanViewModel plan, PlanNodeBox box, Palette p)
    {
        var row = plan.Rows[box.Row];
        double x = box.X, y = box.Y, w = box.Width, h = box.Height;
        const double Radius = 8;
        var rect = new Rect(x, y, w, h);
        var severity = row.Severity switch { PlanSeverity.Critical => p.DangerBrush, PlanSeverity.Alert => p.WarningBrush, _ => p.Fg3Brush };

        if (row.Severity == PlanSeverity.Critical)
            context.DrawRectangle(null, new Pen(new SolidColorBrush(p.Danger, 0.25), 3), new Rect(x - 2, y - 2, w + 4, h + 4), Radius + 2, Radius + 2);

        var selected = ReferenceEquals(row, plan.SelectedRow);
        var border = selected ? new Pen(p.AccentBrush, 2) : row.Severity != PlanSeverity.Normal ? new Pen(severity, 1.5) : new Pen(p.BorderBrush, 1);
        context.DrawRectangle(p.RaisedBrush, border, rect, Radius, Radius);

        DrawIcon(context, row.Node.Kind, x + Pad, y + 11, new Pen(p.Fg2Brush, 1.3));
        context.DrawText(Layout(row.Label, 13, false, true, w - 44, p.FgBrush), new Point(x + 34, y + 9));
        if (row.Target is { } target)
            context.DrawText(Layout(target, MonoSize, true, false, w - (2 * Pad), p.Fg2Brush), new Point(x + Pad, y + 31));

        if (row.RelativeShare is { } share)
        {
            var barWidth = w - (2 * Pad);
            context.DrawRectangle(p.BorderSubtleBrush, null, new Rect(x + Pad, y + 54, barWidth, 4), 2, 2);
            if (share > 0)
                context.DrawRectangle(severity, null, new Rect(x + Pad, y + 54, Math.Max(4, barWidth * Math.Clamp(share, 0, 1)), 4), 2, 2);
            context.DrawText(Layout(row.RelativeText, MonoSize, true, false, 80, severity), new Point(x + Pad, y + 64));
            if (row.Node.ActualTimeMs is not null)
            {
                var time = Layout(row.Time, MonoSize, true, false, 120, p.Fg2Brush);
                context.DrawText(time, new Point(x + w - Pad - time.Width, y + 64));
            }
        }

        var line = 0;
        if (row.MisestimateText.Length > 0 && box.ExtraLines > 0)
        {
            var text = UiText.Format("plan.graph.misestimate", row.EstimatedRows, row.ActualRows, "×" + row.MisestimateText[1..]);
            context.DrawText(Layout(text, MonoSize, true, false, w - (2 * Pad), p.WarningBrush), new Point(x + Pad, y + 84 + (20 * line++)));
        }

        foreach (var warning in row.WarningTexts)
        {
            if (line >= box.ExtraLines)
                break;
            context.DrawText(Layout(warning, MonoSize, false, false, w - (2 * Pad), p.WarningBrush), new Point(x + Pad, y + 84 + (20 * line++)));
        }
    }

    // A 14 px glyph per kind of node, the same strokes as the WinUI glyphs.
    private static void DrawIcon(DrawingContext context, PlanNodeKind kind, double x, double y, Pen pen)
    {
        const double S = 14;
        void Line(double x0, double y0, double x1, double y1) => context.DrawLine(pen, new Point(x + x0, y + y0), new Point(x + x1, y + y1));
        switch (kind)
        {
            case PlanNodeKind.TableScan:
                context.DrawRectangle(null, pen, new Rect(x, y + 1, S, S - 2));
                Line(0, 5, S, 5);
                Line(5, 5, 5, S - 1);
                break;
            case PlanNodeKind.IndexScan:
            case PlanNodeKind.IndexSeek:
                Line(2, 2, S - 2, 2);
                Line(2, 7, S - 2, 7);
                Line(2, 12, 8, 12);
                break;
            case PlanNodeKind.Join:
                context.DrawEllipse(null, pen, new Point(x + 5, y + 7), 4.5, 4.5);
                context.DrawEllipse(null, pen, new Point(x + 9, y + 7), 4.5, 4.5);
                break;
            case PlanNodeKind.Aggregate:
                Line(2, 2, 12, 2);
                Line(2, 2, 8, 7);
                Line(8, 7, 2, 12);
                Line(2, 12, 12, 12);
                break;
            case PlanNodeKind.Sort:
                Line(2, 3, 12, 3);
                Line(2, 7, 9, 7);
                Line(2, 11, 6, 11);
                break;
            case PlanNodeKind.Limit:
                Line(2, 3, 12, 3);
                Line(2, 7, 12, 7);
                Line(2, 12, 12, 12);
                break;
            case PlanNodeKind.Filter:
                Line(1, 2, 13, 2);
                Line(1, 2, 6, 8);
                Line(13, 2, 8, 8);
                Line(6, 8, 6, 13);
                Line(8, 8, 8, 12);
                break;
            default:
                context.DrawRectangle(null, pen, new Rect(x + 1, y + 1, S - 2, S - 2), 3, 3);
                break;
        }
    }

    private FormattedText Layout(string text, double size, bool mono, bool semibold, double width, IBrush brush)
    {
        var key = (text, size, mono, semibold, (int)width, RuntimeHelpers.GetHashCode(brush));
        if (_text.TryGetValue(key, out var cached))
            return cached;
        if (_text.Count >= MaxCachedTexts)
            _text.Clear();
        var face = mono ? Face : semibold ? BoldFace : Typeface.Default;
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, brush)
        {
            MaxTextWidth = Math.Max(1, width),
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
        };
        return _text[key] = formatted;
    }

    private PlanNodeBox? HitTest(Point screen)
    {
        if (_plan is null || _layout is null)
            return null;
        var wx = (screen.X - _offsetX) / _plan.Zoom;
        var wy = (screen.Y - _offsetY) / _plan.Zoom;
        foreach (var box in _layout.Nodes)
        {
            if (wx >= box.X && wx <= box.X + box.Width && wy >= box.Y && wy <= box.Y + box.Height)
                return box;
        }

        return null;
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var properties = e.GetCurrentPoint(this).Properties;
        _pressed = _last = e.GetPosition(this);
        _moved = false;
        _panning = properties.IsMiddleButtonPressed || properties.IsLeftButtonPressed;
        if (_panning)
            e.Pointer.Capture(this);
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_panning)
            return;
        var now = e.GetPosition(this);
        if (!_moved && Math.Abs(now.X - _pressed.X) + Math.Abs(now.Y - _pressed.Y) < DragThreshold)
            return;
        _moved = true;
        _offsetX += now.X - _last.X;
        _offsetY += now.Y - _last.Y;
        _last = now;
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_panning)
            return;
        _panning = false;
        e.Pointer.Capture(null);
        if (!_moved && _plan is { } plan && HitTest(e.GetPosition(this)) is { } hit)
            plan.SelectedRow = plan.Rows[hit.Row];
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_plan is not { } plan || e.Delta.Y == 0)
            return;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var at = e.GetPosition(this);
            var old = plan.Zoom;
            plan.Zoom = old * Math.Pow(ZoomStep, Math.Sign(e.Delta.Y));
            var ratio = plan.Zoom / old;
            _offsetX = at.X - ((at.X - _offsetX) * ratio);
            _offsetY = at.Y - ((at.Y - _offsetY) * ratio);
        }
        else
        {
            _offsetY += e.Delta.Y * WheelPan;
        }

        InvalidateVisual();
        e.Handled = true;
    }

    private Palette ResolvePalette()
    {
        Color Get(string name) => this.TryFindResource(name, ActualThemeVariant, out var value) && value is ISolidColorBrush b ? b.Color : Colors.Gray;
        return new Palette(
            Get("SquillBgBaseBrush"), Get("SquillBgRaisedBrush"), Get("SquillFgBrush"), Get("SquillFg2Brush"), Get("SquillFg3Brush"),
            Get("SquillAccentBrush"), Get("SquillWarningBrush"), Get("SquillDangerBrush"), Get("SquillBorderBrush"), Get("SquillBorderStrongBrush"), Get("SquillBorderSubtleBrush"));
    }

    private sealed class Palette(Color background, Color raised, Color fg, Color fg2, Color fg3, Color accent, Color warning, Color danger, Color border, Color edgeStrong, Color borderSubtle)
    {
        public Color Background { get; } = background;

        public Color Warning { get; } = warning;

        public Color Danger { get; } = danger;

        public Color EdgeStrong { get; } = edgeStrong;

        public Color BorderSubtle { get; } = borderSubtle;

        public IBrush BackgroundBrush { get; } = new SolidColorBrush(background);

        public IBrush RaisedBrush { get; } = new SolidColorBrush(raised);

        public IBrush FgBrush { get; } = new SolidColorBrush(fg);

        public IBrush Fg2Brush { get; } = new SolidColorBrush(fg2);

        public IBrush Fg3Brush { get; } = new SolidColorBrush(fg3);

        public IBrush AccentBrush { get; } = new SolidColorBrush(accent);

        public IBrush WarningBrush { get; } = new SolidColorBrush(warning);

        public IBrush DangerBrush { get; } = new SolidColorBrush(danger);

        public IBrush BorderBrush { get; } = new SolidColorBrush(border);

        public IBrush EdgeStrongBrush { get; } = new SolidColorBrush(edgeStrong);

        public IBrush BorderSubtleBrush { get; } = new SolidColorBrush(borderSubtle);
    }
}
