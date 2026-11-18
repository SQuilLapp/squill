using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Squill.App.Avalonia.Views;
using Squill.Core.Security;
using Squill.Presentation.Localization;
using Squill.Presentation.Security;

namespace Squill.App.Avalonia.Controls;

/// <summary>The privilege matrix of the security tab (mockup M11), drawn in one pass: a header row, then one row per object with one drawn checkbox per privilege.</summary>
public sealed class PrivilegeMatrixCanvas : Control
{
    /// <summary>Width of the object name column.</summary>
    internal const double NameWidth = 280;

    /// <summary>Width of a privilege column.</summary>
    internal const double CellWidth = 92;

    /// <summary>Height of a row.</summary>
    internal const double RowHeight = 28;

    /// <summary>Height of the header row.</summary>
    internal const double HeaderHeight = 32;

    // shortcut: only the first rows are drawn and hit (no virtualization); pick fewer schemas to narrow the rest, upgrade to a visible-range draw if a database routinely lists more.
    internal const int MaxRows = 400;

    private static readonly PrivilegeKind[] Kinds = Enum.GetValues<PrivilegeKind>();
    private static readonly Typeface Face = new(ErDiagramCanvas.Mono);
    private static readonly Typeface BoldFace = new(ErDiagramCanvas.Mono, weight: FontWeight.Bold);

    private SecurityViewModel? _vm;
    private int _row;
    private int _column;

    /// <summary>Creates the control; set <see cref="ViewModel"/> to draw it.</summary>
    public PrivilegeMatrixCanvas()
    {
        Focusable = true;
        ClipToBounds = true;
        GotFocus += (_, _) => InvalidateVisual();
        LostFocus += (_, _) => InvalidateVisual();
    }

    /// <summary>Gets or sets the security tab whose rows are drawn; the caller calls <see cref="Refresh"/> after its rows changed.</summary>
    public SecurityViewModel? ViewModel
    {
        get => _vm;
        set
        {
            _vm = value;
            _row = _column = 0;
            Refresh();
        }
    }

    /// <summary>Gets the row of the focused cell.</summary>
    internal int FocusRow => _row;

    /// <summary>Gets the column of the focused cell.</summary>
    internal int FocusColumn => _column;

    private int RowCount => Math.Min(_vm?.Rows.Count ?? 0, MaxRows);

    /// <summary>Re-measures and redraws after the rows or their cells changed.</summary>
    public void Refresh()
    {
        _row = Math.Clamp(_row, 0, Math.Max(RowCount - 1, 0));
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) =>
        new(NameWidth + (CellWidth * Kinds.Length), RowCount == 0 ? 0 : HeaderHeight + (RowCount * RowHeight));

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_vm is not { } vm || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        Focus();
        var point = e.GetPosition(this);
        if (SecurityFormat.HitTest(point.X, point.Y - HeaderHeight, NameWidth, CellWidth, RowHeight, RowCount, Kinds.Length) is not var (row, column))
            return;
        (_row, _column) = (row, column);
        vm.ToggleCell(row, column);
        Refresh();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        string? tip = null;
        if (_vm is { } vm)
        {
            var point = e.GetPosition(this);
            if (SecurityFormat.HitTest(point.X, point.Y - HeaderHeight, NameWidth, CellWidth, RowHeight, RowCount, Kinds.Length) is var (row, column) && vm.Rows[row].Cells[column].Tooltip is { Length: > 0 } text)
                tip = text;
        }

        ToolTip.SetTip(this, tip);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_vm is not { } vm)
            return;
        if (e.Key == Key.Space)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                vm.ToggleRow(_row);
            else
                vm.ToggleCell(_row, _column);
            Refresh();
            e.Handled = true;
            return;
        }

        var (rowStep, columnStep) = e.Key switch
        {
            Key.Up => (-1, 0),
            Key.Down => (1, 0),
            Key.Left => (0, -1),
            Key.Right => (0, 1),
            _ => (0, 0),
        };
        if (rowStep == 0 && columnStep == 0)
            return;
        (_row, _column) = SecurityFormat.Move(rowStep, columnStep, _row, _column, RowCount, Kinds.Length);
        InvalidateVisual();
        e.Handled = true;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_vm is not { } vm || RowCount == 0)
            return;
        var muted = Brush("SquillFg2Brush");
        var text = Brush("SquillFgBrush");
        DrawText(context, UiText.Get("security.col.object"), 0, 0, NameWidth, HeaderHeight, muted, BoldFace, 11, false);
        for (var c = 0; c < Kinds.Length; c++)
            DrawText(context, Kinds[c].ToString().ToUpperInvariant(), NameWidth + (c * CellWidth), 0, CellWidth, HeaderHeight, muted, BoldFace, 11, true);
        for (var r = 0; r < RowCount; r++)
        {
            var y = HeaderHeight + (r * RowHeight);
            var row = vm.Rows[r];
            DrawText(context, $"{row.Schema}.{row.Name}", 0, y, NameWidth - 12, RowHeight, text, Face, 13, false);
            for (var c = 0; c < Kinds.Length; c++)
            {
                var cell = new Rect(NameWidth + (c * CellWidth), y, CellWidth, RowHeight);
                DrawBox(context, cell.Center, row.Cells[c].Display);
                if (IsFocused && r == _row && c == _column)
                    context.DrawRectangle(null, new Pen(Brush("SquillAccentBrush"), 2), cell.Deflate(new Thickness(24, 2)), 4, 4);
            }
        }
    }

    // The drawn checkbox: filled when held, outlined when pending or empty, a check or a minus inside.
    private void DrawBox(DrawingContext context, Point center, PrivilegeCellDisplay display)
    {
        var box = new Rect(center.X - 8, center.Y - 8, 16, 16);
        var fill = SecurityFormat.FillBrush(display);
        context.DrawRectangle(fill is null ? null : Brush(fill), new Pen(Brush(SecurityFormat.OutlineBrush(display)), 1.5), box, 3, 3);
        var glyph = SecurityFormat.Glyph(display);
        if (glyph.Length > 0)
            DrawText(context, glyph, box.X, box.Y, box.Width, box.Height, Brush(fill is null ? "SquillDangerBrush" : "SquillOnAccentBrush"), BoldFace, 11, true);
    }

    private static void DrawText(DrawingContext context, string value, double x, double y, double width, double height, IBrush brush, Typeface face, double size, bool centered)
    {
        var formatted = new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, size, brush) { MaxTextWidth = Math.Max(width, 1), MaxTextHeight = height, Trimming = TextTrimming.CharacterEllipsis };
        var left = centered ? x + ((width - formatted.WidthIncludingTrailingWhitespace) / 2) : x;
        context.DrawText(formatted, new Point(left, y + ((height - formatted.Height) / 2)));
    }

    private IBrush Brush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Magenta;
}
