using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Squill.App.Avalonia.Views;
using Squill.Presentation.Compare;

namespace Squill.App.Avalonia.Controls;

/// <summary>One side of the schema compare DDL: line numbers, <c>+</c> / <c>-</c> marks, tinted rows and hatched fillers, drawn from <see cref="AlignedLine"/>s. Two panes share their scroll position through <see cref="Scrolled"/>.</summary>
public sealed class DdlPane : Grid
{
    internal const double RowHeight = 24;
    private const double NumberWidth = 48;
    private const double MarkWidth = 20;
    private const double FontSize = 13;

    private static readonly Typeface Face = new(ErDiagramCanvas.Mono);

    private readonly Surface _surface;
    private readonly ScrollBar _bar = new() { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Right, SmallChange = RowHeight, LargeChange = RowHeight * 8 };
    private IReadOnlyList<AlignedLine> _lines = [];
    private bool _syncing;

    /// <summary>Creates a pane.</summary>
    /// <param name="sourceSide"><see langword="true"/> for the source (left) pane, <see langword="false"/> for the target pane.</param>
    public DdlPane(bool sourceSide)
    {
        _surface = new Surface(this, sourceSide);
        Children.Add(_surface);
        Children.Add(_bar);
        _bar.Scroll += (_, e) => SetOffset(e.NewValue, fromBar: true);
        _surface.SizeChanged += (_, _) => SyncBar();
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
            Highlight = -1;
            SetOffset(0, fromBar: false);
            SyncBar();
        }
    }

    internal int Highlight { get; private set; } = -1;

    private double MaxOffset => Math.Max(0, (_lines.Count * RowHeight) - _surface.Bounds.Height);

    /// <summary>Scrolls so that a row is visible and outlines it (the current difference of <c>F7</c>); <c>-1</c> clears it.</summary>
    /// <param name="row">Zero-based aligned row.</param>
    public void Reveal(int row)
    {
        Highlight = row;
        if (row >= 0)
        {
            var top = row * RowHeight;
            var height = _surface.Bounds.Height;
            if (top < Offset || top + RowHeight > Offset + height)
                SetOffset(top - (height / 3), fromBar: false);
        }

        _surface.InvalidateVisual();
    }

    /// <summary>Moves to an offset given by the other pane without raising <see cref="Scrolled"/>.</summary>
    /// <param name="offset">The offset in pixels.</param>
    public void Follow(double offset)
    {
        _syncing = true;
        SetOffset(offset, fromBar: false);
        _syncing = false;
    }

    private void SyncBar()
    {
        _bar.Maximum = MaxOffset;
        _bar.ViewportSize = _surface.Bounds.Height;
        _bar.IsVisible = MaxOffset > 0;
        _bar.Value = Offset;
    }

    private void SetOffset(double value, bool fromBar)
    {
        var clamped = Math.Clamp(value, 0, MaxOffset);
        if (Math.Abs(clamped - Offset) < double.Epsilon && !_syncing && _lines.Count > 0)
            return;
        Offset = clamped;
        if (!fromBar)
            _bar.Value = clamped;
        _surface.InvalidateVisual();
        if (!_syncing)
            Scrolled?.Invoke(this, EventArgs.Empty);
    }

    private IBrush Brush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Magenta;

    private sealed class Surface : Control
    {
        private readonly DdlPane _owner;
        private readonly bool _sourceSide;

        public Surface(DdlPane owner, bool sourceSide)
        {
            _owner = owner;
            _sourceSide = sourceSide;
            ClipToBounds = true;
            ActualThemeVariantChanged += (_, _) => InvalidateVisual();
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            if (e.Delta.Y == 0)
                return;
            _owner.SetOffset(_owner.Offset - (Math.Sign(e.Delta.Y) * RowHeight * 3), fromBar: false);
            e.Handled = true;
        }

        public override void Render(DrawingContext context)
        {
            var bounds = new Rect(Bounds.Size);
            context.FillRectangle(_owner.Brush("SquillBgSurfaceBrush"), bounds);
            var lines = _owner._lines;
            var first = (int)(_owner.Offset / RowHeight);
            var last = Math.Min(lines.Count - 1, (int)((_owner.Offset + bounds.Height) / RowHeight));
            if (first > last)
                return;
            var text = _owner.Brush("SquillFgBrush");
            var muted = _owner.Brush("SquillFg3Brush");
            var hatch = new Pen(_owner.Brush("SquillBorderSubtleBrush"), 1);
            for (var i = first; i <= last; i++)
            {
                var top = (i * RowHeight) - _owner.Offset;
                var row = new Rect(0, top, bounds.Width, RowHeight);
                var (number, line, kind) = CompareFormat.Row(lines[i], _sourceSide);
                switch (kind)
                {
                    case DdlRowKind.Added:
                        context.FillRectangle(_owner.Brush("SquillCellAddedBrush"), row);
                        break;
                    case DdlRowKind.Removed:
                        context.FillRectangle(_owner.Brush("SquillCellDeletedBrush"), row);
                        break;
                    case DdlRowKind.Filler:
                        using (context.PushClip(row))
                        {
                            for (var x = -RowHeight; x < bounds.Width; x += 8)
                                context.DrawLine(hatch, new Point(x, top + RowHeight), new Point(x + RowHeight, top));
                        }

                        break;
                }

                if (number is { } n)
                    Draw(context, n.ToString(CultureInfo.InvariantCulture), muted, NumberWidth - 8, top, right: true);
                var mark = CompareFormat.Mark(kind);
                if (mark.Length > 0)
                    Draw(context, mark, _owner.Brush(kind == DdlRowKind.Added ? "SquillSuccessBrush" : "SquillDangerBrush"), NumberWidth, top, right: false);
                if (line is not null)
                    Draw(context, line, text, NumberWidth + MarkWidth, top, right: false);
                if (i == _owner.Highlight)
                    context.DrawRectangle(null, new Pen(_owner.Brush("SquillAccentBrush"), 1.5), row.Deflate(0.5));
            }
        }

        // Right-aligned text ends at x; left-aligned text starts there.
        private static void Draw(DrawingContext context, string value, IBrush brush, double x, double top, bool right)
        {
            var formatted = new FormattedText(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, FontSize, brush);
            context.DrawText(formatted, new Point(right ? x - formatted.Width : x, top + ((RowHeight - formatted.Height) / 2)));
        }
    }
}
