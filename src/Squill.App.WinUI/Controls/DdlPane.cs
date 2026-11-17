using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Squill.App.WinUI.Views;
using Squill.Presentation.Compare;
using Windows.UI;

namespace Squill.App.WinUI.Controls;

/// <summary>One side of the schema compare DDL: line numbers, <c>+</c> / <c>-</c> marks, tinted rows and hatched fillers, drawn from <see cref="AlignedLine"/>s. Two panes share their scroll position through <see cref="Scrolled"/>.</summary>
public sealed partial class DdlPane : Grid
{
    private const float RowHeight = 24;
    private const float NumberWidth = 48;
    private const float MarkWidth = 20;
    private const float FontSize = 13;

    private static readonly string MonoFamily = ErDiagramCanvas.ResolveMonoFamily();

    private readonly CanvasControl _canvas = new();
    private readonly ScrollBar _bar = new() { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Right, SmallChange = RowHeight, LargeChange = RowHeight * 8 };
    private readonly bool _sourceSide;
    private IReadOnlyList<AlignedLine> _lines = [];
    private int _highlight = -1;
    private bool _syncing;

    /// <summary>Creates a pane.</summary>
    /// <param name="sourceSide"><see langword="true"/> for the source (left) pane, <see langword="false"/> for the target pane.</param>
    public DdlPane(bool sourceSide)
    {
        _sourceSide = sourceSide;
        Children.Add(_canvas);
        Children.Add(_bar);
        _canvas.Draw += OnDraw;
        _canvas.SizeChanged += (_, _) => SyncBar();
        _canvas.PointerWheelChanged += OnWheel;
        _bar.Scroll += (_, _) => SetOffset(_bar.Value, fromBar: true);
        ActualThemeChanged += (_, _) => _canvas.Invalidate();
        Unloaded += (_, _) => _canvas.RemoveFromVisualTree();
    }

    /// <summary>Raised when the vertical offset changes, so the other pane can follow.</summary>
    public event EventHandler? Scrolled;

    /// <summary>Gets the vertical offset in pixels.</summary>
    public double Offset { get; private set; }

    /// <summary>Gets the number of rows drawn.</summary>
    public int RowCount => _lines.Count;

    /// <summary>Gets or sets the rows shown; the scroll position goes back to the top.</summary>
    public IReadOnlyList<AlignedLine> Lines
    {
        get => _lines;
        set
        {
            _lines = value;
            _highlight = -1;
            SetOffset(0, fromBar: false);
            SyncBar();
        }
    }

    /// <summary>Scrolls so that a row is visible and outlines it (the current difference of <c>F7</c>); <c>-1</c> clears it.</summary>
    /// <param name="row">Zero-based aligned row.</param>
    public void Reveal(int row)
    {
        _highlight = row;
        if (row >= 0)
        {
            var top = row * RowHeight;
            var height = (float)_canvas.ActualHeight;
            if (top < Offset || top + RowHeight > Offset + height)
                SetOffset(top - height / 3, fromBar: false);
        }

        _canvas.Invalidate();
    }

    /// <summary>Moves to an offset given by the other pane without raising <see cref="Scrolled"/>.</summary>
    /// <param name="offset">The offset in pixels.</param>
    public void Follow(double offset)
    {
        _syncing = true;
        SetOffset(offset, fromBar: false);
        _syncing = false;
    }

    private double MaxOffset => Math.Max(0, _lines.Count * RowHeight - _canvas.ActualHeight);

    private void SyncBar()
    {
        _bar.Maximum = MaxOffset;
        _bar.ViewportSize = _canvas.ActualHeight;
        _bar.Visibility = MaxOffset > 0 ? Visibility.Visible : Visibility.Collapsed;
        _bar.Value = Offset;
    }

    private void SetOffset(double value, bool fromBar)
    {
        var clamped = Math.Clamp(value, 0, MaxOffset);
        if (clamped == Offset && fromBar == false && !_syncing && _lines.Count > 0)
            return;
        Offset = clamped;
        if (!fromBar)
            _bar.Value = clamped;
        _canvas.Invalidate();
        if (!_syncing)
            Scrolled?.Invoke(this, EventArgs.Empty);
    }

    private void OnWheel(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(_canvas).Properties.MouseWheelDelta;
        if (delta == 0)
            return;
        SetOffset(Offset - Math.Sign(delta) * RowHeight * 3, fromBar: false);
        e.Handled = true;
    }

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var ds = args.DrawingSession;
        Color Get(string brush) => Tokens.Color(this, brush);
        ds.Clear(Get("SquillBgSurfaceBrush"));
        var width = (float)sender.ActualWidth;
        var height = (float)sender.ActualHeight;
        var first = (int)(Offset / RowHeight);
        var last = Math.Min(_lines.Count - 1, (int)((Offset + height) / RowHeight));
        if (first > last)
            return;
        using var mono = new CanvasTextFormat { FontSize = FontSize, FontFamily = MonoFamily, WordWrapping = CanvasWordWrapping.NoWrap, VerticalAlignment = CanvasVerticalAlignment.Center };
        using var numbers = new CanvasTextFormat { FontSize = FontSize, FontFamily = MonoFamily, HorizontalAlignment = CanvasHorizontalAlignment.Right, VerticalAlignment = CanvasVerticalAlignment.Center, WordWrapping = CanvasWordWrapping.NoWrap };
        var text = Get("SquillFgBrush");
        var muted = Get("SquillFg3Brush");
        var border = Get("SquillBorderSubtleBrush");
        for (var i = first; i <= last; i++)
        {
            var top = (float)(i * RowHeight - Offset);
            var (number, line, kind) = CompareFormat.Row(_lines[i], _sourceSide);
            switch (kind)
            {
                case DdlRowKind.Added:
                    ds.FillRectangle(0, top, width, RowHeight, Get("SquillCellAddedBrush"));
                    break;
                case DdlRowKind.Removed:
                    ds.FillRectangle(0, top, width, RowHeight, Get("SquillCellDeletedBrush"));
                    break;
                case DdlRowKind.Filler:
                    Hatch(ds, top, width, border);
                    break;
            }

            if (number is { } n)
                ds.DrawText(n.ToString(System.Globalization.CultureInfo.InvariantCulture), 0, top, NumberWidth - 8, RowHeight, muted, numbers);
            var mark = CompareFormat.Mark(kind);
            if (mark.Length > 0)
                ds.DrawText(mark, NumberWidth, top, MarkWidth, RowHeight, Get(kind == DdlRowKind.Added ? "SquillSuccessBrush" : "SquillDangerBrush"), mono);
            if (line is not null)
                ds.DrawText(line, NumberWidth + MarkWidth, top, Math.Max(0, width - NumberWidth - MarkWidth), RowHeight, text, mono);
            if (i == _highlight)
                ds.DrawRectangle(0.5f, top + 0.5f, width - 1, RowHeight - 1, Get("SquillAccentBrush"), 1.5f);
        }
    }

    // Diagonal strokes clipped to the row: the filler of a line that exists on the other side only.
    private static void Hatch(CanvasDrawingSession ds, float top, float width, Color color)
    {
        using var layer = ds.CreateLayer(1f, new Windows.Foundation.Rect(0, top, width, RowHeight));
        for (var x = -RowHeight; x < width; x += 8)
            ds.DrawLine(x, top + RowHeight, x + RowHeight, top, color, 1);
    }
}
