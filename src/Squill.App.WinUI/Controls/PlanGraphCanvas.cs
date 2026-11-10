using System.ComponentModel;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Squill.Core.Plans;
using Squill.Presentation.Localization;
using Squill.Presentation.Plans;
using Squill.Presentation.Results;
using Windows.System;
using Windows.UI;

namespace Squill.App.WinUI.Controls;

/// <summary>Custom-drawn query plan graph on Win2D: node cards, pills and edges of a <see cref="PlanLayoutResult"/> under the zoom of a <see cref="PlanViewModel"/> and a pan the canvas keeps itself.</summary>
public sealed partial class PlanGraphCanvas : Grid, IDisposable
{
    private const float DotPitch = 16;
    private const float Pad = 12;
    private const double ZoomStep = 1.1;
    private const double DragThreshold = 3;
    private const float Unbounded = 10_000;
    private const float MonoSize = 12;

    private static readonly string MonoFamily = ErDiagramCanvas.ResolveMonoFamily();

    private readonly CanvasControl _canvas = new();
    private readonly LruCache<string, CanvasTextLayout> _text = new(4096, layout => layout.Dispose());
    private CanvasRenderTarget? _dotTile;
    private CanvasImageBrush? _dotBrush;
    private PlanViewModel? _plan;
    private PlanLayoutResult? _layout;
    private Palette? _palette;
    private double _offsetX;
    private double _offsetY;
    private Vector2 _pressed;
    private Vector2 _last;
    private bool _panning;
    private bool _moved;

    /// <summary>Creates the canvas.</summary>
    public PlanGraphCanvas()
    {
        Children.Add(_canvas);
        _canvas.Draw += OnDraw;
        _canvas.CreateResources += (_, _) =>
        {
            _text.Clear();
            ReleaseDots();
        };
        _canvas.PointerPressed += OnPointerPressed;
        _canvas.PointerMoved += OnPointerMoved;
        _canvas.PointerReleased += OnPointerReleased;
        _canvas.PointerCanceled += OnPointerReleased;
        _canvas.PointerWheelChanged += OnPointerWheelChanged;
        _canvas.DoubleTapped += (_, _) => FitWidth();
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
            _offsetX = 0;
            _offsetY = 0;
            _layout = null;
            if (_plan is not null)
            {
                _plan.PropertyChanged += OnPlanChanged;
                _layout = PlanLayout.Compute(_plan.Rows, MeasureText);
            }

            _canvas.Invalidate();
        }
    }

    /// <summary>Zooms to fit the width of the plan into the canvas, never beyond 100 %.</summary>
    public void FitWidth()
    {
        if (_plan is null || _layout is not { Width: > 0 } layout || _canvas.ActualWidth <= 0)
            return;
        _plan.Zoom = Math.Min(1, _canvas.ActualWidth / layout.Width);
        _offsetX = 0;
        _offsetY = 0;
        _canvas.Invalidate();
    }

    /// <summary>Releases the cached text layouts and the dot tile; the canvas also does it when it is unloaded.</summary>
    public void Dispose()
    {
        _text.Clear();
        ReleaseDots();
    }

    private static double MeasureText(string text)
    {
        using var format = new CanvasTextFormat { FontSize = MonoSize, FontFamily = MonoFamily, WordWrapping = CanvasWordWrapping.NoWrap };
        using var layout = new CanvasTextLayout(CanvasDevice.GetSharedDevice(), text, format, Unbounded, Unbounded);
        return layout.LayoutBounds.Width;
    }

    private void OnPlanChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlanViewModel.SelectedRow))
            RevealSelected();
        _canvas.Invalidate();
    }

    // The table (or a click) selected a node: pan so that its card is visible.
    private void RevealSelected()
    {
        if (_plan is null || _layout is null || _canvas.ActualWidth <= 0)
            return;
        var zoom = (float)_plan.Zoom;
        foreach (var box in _layout.Nodes)
        {
            if (!ReferenceEquals(_plan.Rows[box.Row], _plan.SelectedRow))
                continue;
            var left = box.X * zoom + _offsetX;
            var top = box.Y * zoom + _offsetY;
            var right = left + (box.Width * zoom);
            var bottom = top + (box.Height * zoom);
            if (left < 0 || right > _canvas.ActualWidth)
                _offsetX = (_canvas.ActualWidth / 2) - ((box.X + (box.Width / 2)) * zoom);
            if (top < 0 || bottom > _canvas.ActualHeight)
                _offsetY = (_canvas.ActualHeight / 2) - ((box.Y + (box.Height / 2)) * zoom);
            return;
        }
    }

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var ds = args.DrawingSession;
        var p = _palette ??= ResolvePalette();
        ds.Clear(p.Background);
        ds.FillRectangle(0, 0, (float)sender.ActualWidth, (float)sender.ActualHeight, DotBrush(sender, p));
        if (_plan is not { } plan || _layout is not { } layout)
            return;

        var zoom = (float)plan.Zoom;
        ds.Transform = Matrix3x2.CreateScale(zoom) * Matrix3x2.CreateTranslation((float)_offsetX, (float)_offsetY);
        // Only what intersects the viewport is drawn, so plans with hundreds of nodes stay smooth.
        var view = new Windows.Foundation.Rect(-_offsetX / zoom, -_offsetY / zoom, sender.ActualWidth / zoom, sender.ActualHeight / zoom);

        foreach (var edge in layout.Edges)
        {
            var minX = Math.Min(edge.X0, edge.X1);
            var maxX = Math.Max(edge.X0, edge.X1);
            var minY = Math.Min(Math.Min(edge.Y0, edge.Y1), Math.Min(edge.C1Y, edge.C2Y));
            var maxY = Math.Max(Math.Max(edge.Y0, edge.Y1), Math.Max(edge.C1Y, edge.C2Y));
            if (maxX < view.Left || minX > view.Right || maxY < view.Top || minY > view.Bottom)
                continue;
            using var path = new CanvasPathBuilder(sender);
            path.BeginFigure((float)edge.X0, (float)edge.Y0);
            path.AddCubicBezier(new Vector2((float)edge.C1X, (float)edge.C1Y), new Vector2((float)edge.C2X, (float)edge.C2Y), new Vector2((float)edge.X1, (float)edge.Y1));
            path.EndFigure(CanvasFigureLoop.Open);
            using var geometry = CanvasGeometry.CreatePath(path);
            var color = edge.Severity switch { PlanSeverity.Critical => p.Danger, PlanSeverity.Alert => p.Warning, _ => p.EdgeStrong };
            color.A = (byte)(color.A * 0.55);
            ds.DrawGeometry(geometry, color, (float)edge.Thickness, new CanvasStrokeStyle { StartCap = CanvasCapStyle.Round, EndCap = CanvasCapStyle.Round });
        }

        foreach (var pill in layout.Pills)
        {
            if (pill.X + pill.Width < view.Left || pill.X > view.Right || pill.Y + pill.Height < view.Top || pill.Y > view.Bottom)
                continue;
            var rect = new Windows.Foundation.Rect(pill.X, pill.Y, pill.Width, pill.Height);
            ds.FillRoundedRectangle(rect, 11, 11, p.Background);
            ds.DrawRoundedRectangle(rect, 11, 11, p.EdgeStrong, 1, new CanvasStrokeStyle { DashStyle = CanvasDashStyle.Dash });
            var label = Layout(sender, pill.Text, MonoSize, true, false, (float)pill.Width - 8, 16);
            ds.DrawTextLayout(label, (float)(pill.X + 4), (float)(pill.Y + ((pill.Height - 16) / 2)), p.Fg2);
        }

        foreach (var box in layout.Nodes)
        {
            if (box.X + box.Width < view.Left || box.X > view.Right || box.Y + box.Height < view.Top || box.Y > view.Bottom)
                continue;
            DrawCard(sender, ds, plan, box, p);
        }

        ds.Transform = Matrix3x2.Identity;
    }

    private void DrawCard(CanvasControl sender, CanvasDrawingSession ds, PlanViewModel plan, PlanNodeBox box, Palette p)
    {
        var row = plan.Rows[box.Row];
        float x = (float)box.X, y = (float)box.Y, w = (float)box.Width, h = (float)box.Height;
        var radius = 8f;
        var rect = new Windows.Foundation.Rect(x, y, w, h);
        var severity = row.Severity switch { PlanSeverity.Critical => p.Danger, PlanSeverity.Alert => p.Warning, _ => p.Fg3 };

        if (row.Severity == PlanSeverity.Critical)
        {
            var glow = p.Danger;
            glow.A = 64;
            ds.DrawRoundedRectangle(new Windows.Foundation.Rect(x - 2, y - 2, w + 4, h + 4), radius + 2, radius + 2, glow, 3);
        }

        ds.FillRoundedRectangle(rect, radius, radius, p.Raised);
        var selected = ReferenceEquals(row, plan.SelectedRow);
        if (selected)
            ds.DrawRoundedRectangle(rect, radius, radius, p.Accent, 2);
        else if (row.Severity != PlanSeverity.Normal)
            ds.DrawRoundedRectangle(rect, radius, radius, severity, 1.5f);
        else
            ds.DrawRoundedRectangle(rect, radius, radius, p.Border, 1);

        DrawIcon(ds, row.Node.Kind, x + Pad, y + 11, p.Fg2);
        ds.DrawTextLayout(Layout(sender, row.Label, 13, false, true, w - 44, 18), x + 34, y + 9, p.Fg);
        if (row.Target is { } target)
            ds.DrawTextLayout(Layout(sender, target, MonoSize, true, false, w - (2 * Pad), 16), x + Pad, y + 31, p.Fg2);

        if (row.RelativeShare is { } share)
        {
            var barWidth = w - (2 * Pad);
            ds.FillRoundedRectangle(x + Pad, y + 54, barWidth, 4, 2, 2, p.BorderSubtle);
            if (share > 0)
                ds.FillRoundedRectangle(x + Pad, y + 54, Math.Max(4, barWidth * (float)Math.Clamp(share, 0, 1)), 4, 2, 2, severity);
            ds.DrawTextLayout(Layout(sender, row.RelativeText, MonoSize, true, false, 80, 16), x + Pad, y + 64, severity);
            if (row.Node.ActualTimeMs is not null)
            {
                var time = Layout(sender, row.Time, MonoSize, true, false, 120, 16);
                ds.DrawTextLayout(time, x + w - Pad - (float)time.LayoutBounds.Width, y + 64, p.Fg2);
            }
        }

        var line = 0;
        if (row.MisestimateText.Length > 0 && box.ExtraLines > 0)
        {
            var text = UiText.Format("plan.graph.misestimate", row.EstimatedRows, row.ActualRows, "×" + row.MisestimateText[1..]);
            ds.DrawTextLayout(Layout(sender, text, MonoSize, true, false, w - (2 * Pad), 16), x + Pad, y + 84 + (20 * line++), p.Warning);
        }

        foreach (var warning in row.WarningTexts)
        {
            if (line >= box.ExtraLines)
                break;
            ds.DrawTextLayout(Layout(sender, warning, MonoSize, false, false, w - (2 * Pad), 16), x + Pad, y + 84 + (20 * line++), p.Warning);
        }
    }

    // A 14 px glyph per kind of node, drawn with primitives (the XAML path icons of Icons.cs cannot be used in Win2D without a path parser).
    private static void DrawIcon(CanvasDrawingSession ds, PlanNodeKind kind, float x, float y, Color color)
    {
        const float S = 14;
        switch (kind)
        {
            case PlanNodeKind.TableScan:
                ds.DrawRectangle(x, y + 1, S, S - 2, color, 1.3f);
                ds.DrawLine(x, y + 5, x + S, y + 5, color, 1.3f);
                ds.DrawLine(x + 5, y + 5, x + 5, y + S - 1, color, 1.3f);
                break;
            case PlanNodeKind.IndexScan:
            case PlanNodeKind.IndexSeek:
                ds.DrawLine(x + 2, y + 2, x + S - 2, y + 2, color, 1.3f);
                ds.DrawLine(x + 2, y + 7, x + S - 2, y + 7, color, 1.3f);
                ds.DrawLine(x + 2, y + 12, x + 8, y + 12, color, 1.3f);
                break;
            case PlanNodeKind.Join:
                ds.DrawCircle(x + 5, y + 7, 4.5f, color, 1.3f);
                ds.DrawCircle(x + 9, y + 7, 4.5f, color, 1.3f);
                break;
            case PlanNodeKind.Aggregate:
                ds.DrawLine(x + 2, y + 2, x + 12, y + 2, color, 1.3f);
                ds.DrawLine(x + 2, y + 2, x + 8, y + 7, color, 1.3f);
                ds.DrawLine(x + 8, y + 7, x + 2, y + 12, color, 1.3f);
                ds.DrawLine(x + 2, y + 12, x + 12, y + 12, color, 1.3f);
                break;
            case PlanNodeKind.Sort:
                ds.DrawLine(x + 2, y + 3, x + 12, y + 3, color, 1.3f);
                ds.DrawLine(x + 2, y + 7, x + 9, y + 7, color, 1.3f);
                ds.DrawLine(x + 2, y + 11, x + 6, y + 11, color, 1.3f);
                break;
            case PlanNodeKind.Limit:
                ds.DrawLine(x + 2, y + 3, x + 12, y + 3, color, 1.3f);
                ds.DrawLine(x + 2, y + 7, x + 12, y + 7, color, 1.3f);
                ds.DrawLine(x + 2, y + 12, x + 12, y + 12, color, 1.3f);
                ds.DrawLine(x + 2, y + 9.5f, x + 12, y + 9.5f, color, 0.8f);
                break;
            case PlanNodeKind.Filter:
                ds.DrawLine(x + 1, y + 2, x + 13, y + 2, color, 1.3f);
                ds.DrawLine(x + 1, y + 2, x + 6, y + 8, color, 1.3f);
                ds.DrawLine(x + 13, y + 2, x + 8, y + 8, color, 1.3f);
                ds.DrawLine(x + 6, y + 8, x + 6, y + 13, color, 1.3f);
                ds.DrawLine(x + 8, y + 8, x + 8, y + 12, color, 1.3f);
                break;
            default:
                ds.DrawRoundedRectangle(x + 1, y + 1, S - 2, S - 2, 3, 3, color, 1.3f);
                break;
        }
    }

    private CanvasTextLayout Layout(CanvasControl sender, string text, float size, bool mono, bool semibold, float width, float height)
    {
        var key = $"{size}|{mono}|{semibold}|{width:0}|{text}";
        if (_text.TryGet(key, out var cached))
            return cached;
        using var format = new CanvasTextFormat
        {
            FontSize = size,
            WordWrapping = CanvasWordWrapping.NoWrap,
            TrimmingGranularity = CanvasTextTrimmingGranularity.Character,
            TrimmingSign = CanvasTrimmingSign.Ellipsis,
            FontWeight = semibold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
        };
        if (mono)
            format.FontFamily = MonoFamily;
        var layout = new CanvasTextLayout(sender, text, format, width, height);
        _text.Add(key, layout);
        return layout;
    }

    private void ReleaseDots()
    {
        _dotBrush?.Dispose();
        _dotTile?.Dispose();
        _dotBrush = null;
        _dotTile = null;
    }

    private CanvasImageBrush DotBrush(CanvasControl sender, Palette p)
    {
        if (_dotBrush is not null)
            return _dotBrush;
        _dotTile = new CanvasRenderTarget(sender, DotPitch, DotPitch, 96);
        using (var tile = _dotTile.CreateDrawingSession())
        {
            tile.Clear(Microsoft.UI.Colors.Transparent);
            tile.FillRectangle(8, 8, 1, 1, p.BorderSubtle);
        }

        return _dotBrush = new CanvasImageBrush(sender, _dotTile) { ExtendX = CanvasEdgeBehavior.Wrap, ExtendY = CanvasEdgeBehavior.Wrap };
    }

    private PlanNodeBox? HitTest(Vector2 screen)
    {
        if (_plan is null || _layout is null)
            return null;
        var zoom = (float)_plan.Zoom;
        var wx = (screen.X - _offsetX) / zoom;
        var wy = (screen.Y - _offsetY) / zoom;
        foreach (var box in _layout.Nodes)
        {
            if (wx >= box.X && wx <= box.X + box.Width && wy >= box.Y && wy <= box.Y + box.Height)
                return box;
        }

        return null;
    }

    private Vector2 Position(PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_canvas).Position;
        return new Vector2((float)point.X, (float)point.Y);
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var properties = e.GetCurrentPoint(_canvas).Properties;
        _pressed = _last = Position(e);
        _moved = false;
        _panning = properties.IsMiddleButtonPressed || properties.IsLeftButtonPressed;
        if (_panning)
            _canvas.CapturePointer(e.Pointer);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_panning)
            return;
        var now = Position(e);
        if (!_moved && Vector2.Distance(now, _pressed) < DragThreshold)
            return;
        _moved = true;
        _offsetX += now.X - _last.X;
        _offsetY += now.Y - _last.Y;
        _last = now;
        _canvas.Invalidate();
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_panning)
            return;
        _panning = false;
        _canvas.ReleasePointerCapture(e.Pointer);
        if (!_moved && _plan is { } plan && HitTest(Position(e)) is { } hit)
            plan.SelectedRow = plan.Rows[hit.Row];
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_canvas);
        if (_plan is not { } plan || point.Properties.MouseWheelDelta == 0)
            return;
        if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control))
        {
            var old = plan.Zoom;
            plan.Zoom = old * Math.Pow(ZoomStep, Math.Sign(point.Properties.MouseWheelDelta));
            var ratio = plan.Zoom / old;
            _offsetX = point.Position.X - ((point.Position.X - _offsetX) * ratio);
            _offsetY = point.Position.Y - ((point.Position.Y - _offsetY) * ratio);
        }
        else
        {
            _offsetY += point.Properties.MouseWheelDelta;
        }

        _canvas.Invalidate();
        e.Handled = true;
    }

    private Palette ResolvePalette()
    {
        var key = _canvas.ActualTheme == ElementTheme.Light ? "Light" : "Default";
        var theme = DataGridCanvas.FindThemeDictionary(Application.Current.Resources, key)!;
        Color Get(string name) => (Color)theme[name];
        return new Palette(
            Get("SquillBgBaseColor"), Get("SquillBgRaisedColor"), Get("SquillFgColor"), Get("SquillFg2Color"), Get("SquillFg3Color"),
            Get("SquillAccentColor"), Get("SquillWarningColor"), Get("SquillDangerColor"), Get("SquillBorderColor"), Get("SquillBorderStrongColor"), Get("SquillBorderSubtleColor"));
    }

    private sealed record Palette(Color Background, Color Raised, Color Fg, Color Fg2, Color Fg3, Color Accent, Color Warning, Color Danger, Color Border, Color EdgeStrong, Color BorderSubtle);
}
