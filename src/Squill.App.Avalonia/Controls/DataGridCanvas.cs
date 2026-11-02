using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Squill.Presentation;
using Squill.Presentation.Editing;
using Squill.Presentation.Results;

namespace Squill.App.Avalonia.Controls;

/// <summary>Custom-drawn grid surface: paints only the visible cells and header, with no per-cell or per-row objects.</summary>
public sealed class DataGridCanvas : Control
{
    private const int SampleRows = 100;
    private const double CellPadding = 8;
    private const double ResizeTolerance = 4;

    /// <summary>Identifies the <see cref="DataSource"/> property.</summary>
    public static readonly StyledProperty<IGridDataSource?> DataSourceProperty =
        AvaloniaProperty.Register<DataGridCanvas, IGridDataSource?>(nameof(DataSource));

    /// <summary>Identifies the <see cref="EditController"/> property.</summary>
    public static readonly StyledProperty<GridEditController?> EditControllerProperty =
        AvaloniaProperty.Register<DataGridCanvas, GridEditController?>(nameof(EditController));

    /// <summary>Identifies the <see cref="Selection"/> property.</summary>
    public static readonly StyledProperty<GridSelection> SelectionProperty =
        AvaloniaProperty.Register<DataGridCanvas, GridSelection>(nameof(Selection), new GridSelection());

    private readonly LruCache<(string Text, IBrush Brush, bool Struck), FormattedText> _text = new(4096);
    private readonly GridLayout _layout;
    private readonly Typeface _typeface = new(FontFamily.Default);
    private readonly double _fontSize = 13;
    private IGridDataSource? _source;
    private double _scrollX;
    private double _scrollY;
    private int _measuredRows;
    private bool _userResized;
    private int _resizeColumn = -1;
    private double _resizeStartX;
    private double _resizeStartWidth;
    private GridBrushes? _brushes;
    private bool _selecting;

    /// <summary>Creates the canvas, reading the row height from the token resources.</summary>
    public DataGridCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        ActualThemeVariantChanged += (_, _) =>
        {
            _brushes = null;
            _text.Clear();
            InvalidateVisual();
        };
        _layout = new GridLayout(ResolveDouble("SquillRowHeightStandard", 26), ResolveDouble("SquillRowHeightStandard", 26));
    }

    /// <summary>Raised when the content extent, viewport or scroll offsets changed so hosts can update scroll bars.</summary>
    public event EventHandler? ScrollStateChanged;

    /// <summary>Gets or sets the data shown by the grid.</summary>
    public IGridDataSource? DataSource
    {
        get => GetValue(DataSourceProperty);
        set => SetValue(DataSourceProperty, value);
    }

    /// <summary>Gets or sets the selection; share the one given to <see cref="EditController"/> when editing.</summary>
    public GridSelection Selection
    {
        get => GetValue(SelectionProperty);
        set => SetValue(SelectionProperty, value);
    }

    /// <summary>Gets or sets the cell-edit logic; <see langword="null"/> keeps the grid read-only.</summary>
    public GridEditController? EditController
    {
        get => GetValue(EditControllerProperty);
        set => SetValue(EditControllerProperty, value);
    }

    /// <summary>Gets or sets the service that reports paste errors.</summary>
    public IDialogService? Dialogs { get; set; }

    /// <summary>Gets the rectangle of a cell in canvas coordinates, or <see langword="null"/> when it is scrolled out of the body.</summary>
    /// <param name="row">Grid row index.</param>
    /// <param name="column">Column index.</param>
    /// <returns>The visible cell rectangle.</returns>
    public Rect? CellRect(long row, int column)
    {
        if (column < 0 || column >= _layout.ColumnCount)
        {
            return null;
        }

        var rect = new Rect(
            _layout.ColumnLeft(column) - _scrollX,
            _layout.HeaderHeight + (row * _layout.RowHeight) - _scrollY,
            _layout.ColumnWidth(column),
            _layout.RowHeight);
        var body = new Rect(0, _layout.HeaderHeight, Bounds.Width, BodyHeight);
        return body.Contains(rect.TopLeft) && body.Contains(new Point(rect.Left, rect.Bottom - 0.5)) ? rect : null;
    }

    /// <summary>Gets the geometry helper (read-only use).</summary>
    public GridLayout Layout => _layout;

    /// <summary>Gets or sets the horizontal scroll offset (clamped).</summary>
    public double ScrollX
    {
        get => _scrollX;
        set => SetScroll(value, _scrollY);
    }

    /// <summary>Gets or sets the vertical scroll offset (clamped).</summary>
    public double ScrollY
    {
        get => _scrollY;
        set => SetScroll(_scrollX, value);
    }

    /// <summary>Gets the duration of the last <see cref="Render"/> call, in milliseconds.</summary>
    public double LastRenderMs { get; private set; }

    /// <summary>Gets the visible body height below the header.</summary>
    public double BodyHeight => Math.Max(0, Bounds.Height - _layout.HeaderHeight);

    /// <summary>Gets the highest valid vertical scroll offset.</summary>
    public double MaxScrollY => _layout.MaxScrollY(_source?.RowCount ?? 0, BodyHeight);

    /// <summary>Gets the highest valid horizontal scroll offset.</summary>
    public double MaxScrollX => _layout.MaxScrollX(Bounds.Width);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataSourceProperty)
        {
            if (_source is not null)
            {
                _source.Changed -= OnSourceChanged;
            }

            _source = DataSource;
            if (_source is not null)
            {
                _source.Changed += OnSourceChanged;
            }

            Selection.Clear();
            _userResized = false;
            _measuredRows = -1;
            MeasureColumns();
            SetScroll(0, 0);
            InvalidateVisual();
        }
        else if (change.Property == EditControllerProperty || change.Property == SelectionProperty)
        {
            InvalidateVisual();
        }
        else if (change.Property == BoundsProperty)
        {
            SetScroll(_scrollX, _scrollY);
        }
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var started = Stopwatch.GetTimestamp();
        base.Render(context);
        var b = _brushes ??= ResolveBrushes();
        var width = Bounds.Width;
        context.FillRectangle(b.Surface, new Rect(0, 0, width, Bounds.Height));
        var source = _source;
        if (source is not null && _layout.ColumnCount > 0)
        {
            var (firstCol, endCol) = _layout.VisibleColumns(_scrollX, width);
            var (firstRow, endRow) = _layout.VisibleRows(_scrollY, BodyHeight, source.RowCount);
            var rowHeight = _layout.RowHeight;
            var headerHeight = _layout.HeaderHeight;
            for (var col = firstCol; col < endCol; col++)
            {
                var left = _layout.ColumnLeft(col) - _scrollX;
                var colWidth = _layout.ColumnWidth(col);
                var info = source.Columns[col];
                using (context.PushClip(new Rect(left, 0, colWidth, Bounds.Height)))
                {
                    for (var row = firstRow; row < endRow; row++)
                    {
                        var top = headerHeight + (row * rowHeight) - _scrollY;
                        var edit = EditController;
                        var state = edit?.GetRowState(row) ?? RowState.Unchanged;
                        if (state != RowState.Unchanged)
                        {
                            context.FillRectangle(state == RowState.Inserted ? b.SuccessSoft : state == RowState.Deleted ? b.DangerSoft : b.AccentSoft, new Rect(left, top, colWidth, rowHeight));
                        }

                        if (state != RowState.Inserted && edit?.IsCellModified(row, col) == true)
                        {
                            context.FillRectangle(b.AccentSoft, new Rect(left, top, colWidth, rowHeight));
                        }

                        if (Selection.Contains(row, col, source.RowCount))
                        {
                            context.FillRectangle(b.Selection, new Rect(left, top, colWidth, rowHeight));
                        }

                        var value = CellFormatter.Format(source.GetCell(row, col), info);
                        var muted = value is null || (edit is not null && !edit.IsColumnEditable(col));
                        var text = Text(value ?? "NULL", muted ? b.Muted : b.Text, state == RowState.Deleted);
                        context.DrawText(text, new Point(left + CellPadding, top + ((rowHeight - text.Height) / 2)));
                        if (edit?.IsCellInvalid(row, col) == true)
                        {
                            context.DrawRectangle(null, b.DangerPen, new Rect(left + 0.5, top + 0.5, colWidth - 1, rowHeight - 1));
                        }
                    }

                    context.FillRectangle(b.Raised, new Rect(left, 0, colWidth, headerHeight));
                    if (Selection.ContainsColumn(col))
                    {
                        context.FillRectangle(b.Selection, new Rect(left, 0, colWidth, headerHeight));
                    }

                    var header = Text(info.Name, b.HeaderText);
                    context.DrawText(header, new Point(left + CellPadding, (headerHeight - header.Height) / 2));
                }

                context.DrawLine(b.Border, new Point(left + colWidth - 0.5, 0), new Point(left + colWidth - 0.5, Bounds.Height));
            }

            for (var row = firstRow; row < endRow; row++)
            {
                var y = headerHeight + ((row + 1) * rowHeight) - _scrollY - 0.5;
                context.DrawLine(b.BorderSubtle, new Point(0, y), new Point(width, y));
            }
        }

        context.DrawLine(b.Border, new Point(0, _layout.HeaderHeight - 0.5), new Point(width, _layout.HeaderHeight - 0.5));
        LastRenderMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            SetScroll(_scrollX - (e.Delta.Y * _layout.RowHeight * 3), _scrollY);
        }
        else
        {
            SetScroll(_scrollX - (e.Delta.X * _layout.RowHeight * 3), _scrollY - (e.Delta.Y * _layout.RowHeight * 3));
        }

        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetPosition(this);
        Focus();
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        EditController?.Cancel();
        if (point.Y > _layout.HeaderHeight)
        {
            SelectAt(point, shift);
            if (e.ClickCount == 2 && EditController is { } doubleClicked && doubleClicked.Begin())
            {
                EnsureVisible(Selection.ActiveRow, Selection.ActiveColumn);
            }

            _selecting = true;
            e.Pointer.Capture(this);
            return;
        }

        var column = _layout.ResizeHandleAt(point.X, _scrollX, ResizeTolerance);
        if (column >= 0)
        {
            _resizeColumn = column;
            _resizeStartX = point.X;
            _resizeStartWidth = _layout.ColumnWidth(column);
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }

        var clicked = _layout.ColumnAt(point.X + _scrollX);
        if (clicked >= 0)
        {
            Selection.SelectColumn(clicked, shift);
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (_selecting)
        {
            SelectAt(point, true);
            return;
        }

        if (_resizeColumn >= 0)
        {
            _layout.SetColumnWidth(_resizeColumn, _resizeStartWidth + point.X - _resizeStartX);
            _userResized = true;
            SetScroll(_scrollX, _scrollY);
            InvalidateVisual();
            ScrollStateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        var onHandle = point.Y <= _layout.HeaderHeight && _layout.ResizeHandleAt(point.X, _scrollX, ResizeTolerance) >= 0;
        Cursor = onHandle ? new Cursor(StandardCursorType.SizeWestEast) : null;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_selecting)
        {
            _selecting = false;
            e.Pointer.Capture(null);
        }

        if (_resizeColumn >= 0)
        {
            _resizeColumn = -1;
            e.Pointer.Capture(null);
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var source = _source;
        if (source is null)
        {
            return;
        }

        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (ctrl && e.Key == Key.C)
        {
            _ = CopySelectionAsync(source);
            e.Handled = true;
            return;
        }

        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (EditController is { } edit && ApplyEditKey(edit, e.Key, ctrl, shift))
        {
            e.Handled = true;
            EnsureVisible(Selection.ActiveRow, Selection.ActiveColumn);
            InvalidateVisual();
            return;
        }

        if (ApplyKey(e.Key, ctrl, shift, source))
        {
            e.Handled = true;
            EnsureVisible(Selection.ActiveRow, Selection.ActiveColumn);
            InvalidateVisual();
        }
    }

    /// <inheritdoc />
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (EditController is { } edit && !string.IsNullOrEmpty(e.Text) && !char.IsControl(e.Text[0]) && edit.Begin(e.Text))
        {
            e.Handled = true;
        }
    }

    private bool ApplyEditKey(GridEditController edit, Key key, bool ctrl, bool shift)
    {
        switch (key)
        {
            case Key.F2:
                return edit.Begin();
            case Key.Delete when ctrl:
                edit.DeleteSelectedRows();
                return true;
            case Key.Delete:
                edit.ClearSelection();
                return true;
            case Key.Insert when ctrl:
            case Key.N when ctrl && shift:
                edit.AddRow();
                return true;
            case Key.V when ctrl:
                _ = PasteAsync(edit);
                return true;
            default:
                return false;
        }
    }

    private async Task PasteAsync(GridEditController edit)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard || await clipboard.TryGetTextAsync() is not { } text)
        {
            return;
        }

        var error = edit.Paste(text);
        if (error is not null && Dialogs is { } dialogs)
        {
            await dialogs.ShowErrorAsync("Paste", error);
        }
    }

    private async Task CopySelectionAsync(IGridDataSource source)
    {
        if (Selection.HasSelection && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(Selection.ToTsv(source));
        }
    }

    private bool ApplyKey(Key key, bool ctrl, bool shift, IGridDataSource source)
    {
        var rows = source.RowCount;
        var cols = source.Columns.Count;
        var page = Math.Max(1, (long)(BodyHeight / _layout.RowHeight) - 1);
        (long Rows, long Cols)? delta = key switch
        {
            Key.Up => (-1, 0),
            Key.Down => (1, 0),
            Key.Left => (0, -1),
            Key.Right => (0, 1),
            Key.PageUp => (-page, 0),
            Key.PageDown => (page, 0),
            Key.Home => (ctrl ? -rows : 0, -cols),
            Key.End => (ctrl ? rows : 0, cols),
            _ => null,
        };
        if (delta is { } d)
        {
            Selection.Move(d.Rows, d.Cols, rows, cols, shift);
            return true;
        }

        if (ctrl && key == Key.A)
        {
            Selection.SelectAll(rows, cols);
            return true;
        }

        return false;
    }

    private void SelectAt(Point point, bool extend)
    {
        var source = _source;
        if (source is null || source.RowCount == 0 || source.Columns.Count == 0)
        {
            return;
        }

        var column = _layout.ColumnAt(point.X + _scrollX);
        if (column < 0)
        {
            column = point.X + _scrollX < 0 ? 0 : source.Columns.Count - 1;
        }

        var row = Math.Clamp((long)((point.Y - _layout.HeaderHeight + _scrollY) / _layout.RowHeight), 0, source.RowCount - 1);
        Selection.SelectCell(row, column, extend);
        InvalidateVisual();
    }

    private void EnsureVisible(long row, int column)
    {
        var top = row * _layout.RowHeight;
        var y = top < _scrollY ? top : top + _layout.RowHeight > _scrollY + BodyHeight ? top + _layout.RowHeight - BodyHeight : _scrollY;
        var left = _layout.ColumnLeft(column);
        var right = left + _layout.ColumnWidth(column);
        var x = left < _scrollX ? left : right > _scrollX + Bounds.Width ? right - Bounds.Width : _scrollX;
        SetScroll(x, y);
    }

    private void OnSourceChanged(object? sender, EventArgs e)
    {
        if (!_userResized && _measuredRows < SampleRows && (_source?.RowCount ?? 0) > _measuredRows)
        {
            MeasureColumns();
        }

        SetScroll(_scrollX, _scrollY);
        InvalidateVisual();
        ScrollStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void MeasureColumns()
    {
        var source = _source;
        if (source is null)
        {
            _layout.SetColumnWidths([]);
            return;
        }

        var brush = (_brushes ??= ResolveBrushes()).Text;
        var sample = (int)Math.Min(SampleRows, source.RowCount);
        var widths = new double[source.Columns.Count];
        for (var col = 0; col < widths.Length; col++)
        {
            var max = new FormattedText(source.Columns[col].Name, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _typeface, _fontSize, brush).Width;
            for (var row = 0; row < sample; row++)
            {
                var value = CellFormatter.Format(source.GetCell(row, col), source.Columns[col]) ?? "NULL";
                max = Math.Max(max, Text(value, brush).Width);
            }

            widths[col] = max + (2 * CellPadding);
        }

        _layout.SetColumnWidths(widths);
        _measuredRows = sample;
    }

    private FormattedText Text(string text, IBrush brush, bool struck = false)
    {
        if (!_text.TryGet((text, brush, struck), out var formatted))
        {
            formatted = new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _typeface, _fontSize, brush);
            if (struck)
            {
                formatted.SetTextDecorations(TextDecorations.Strikethrough);
            }

            _text.Add((text, brush, struck), formatted);
        }

        return formatted;
    }

    private void SetScroll(double x, double y)
    {
        var newX = Math.Clamp(x, 0, MaxScrollX);
        var newY = Math.Clamp(y, 0, MaxScrollY);
        if (Math.Abs(newX - _scrollX) > 0.001 || Math.Abs(newY - _scrollY) > 0.001)
        {
            _scrollX = newX;
            _scrollY = newY;
            InvalidateVisual();
        }

        ScrollStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private GridBrushes ResolveBrushes() => new(
        ResolveBrush("SquillBgSurfaceBrush"),
        ResolveBrush("SquillBgRaisedBrush"),
        ResolveBrush("SquillFgBrush"),
        ResolveBrush("SquillFg2Brush"),
        ResolveBrush("SquillFg3Brush"),
        new Pen(ResolveBrush("SquillBorderBrush")),
        new Pen(ResolveBrush("SquillBorderSubtleBrush")),
        ResolveBrush("SquillSelectionBrush"),
        ResolveBrush("SquillAccentSoftBrush"),
        ResolveBrush("SquillSuccessSoftBrush"),
        ResolveBrush("SquillDangerSoftBrush"),
        new Pen(ResolveBrush("SquillDangerBrush"), 1.5));

    private IBrush ResolveBrush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : FallbackBrush;

    private double ResolveDouble(string key, double fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is double d ? d : fallback;

    private static readonly IBrush FallbackBrush = Brushes.Gray;

    private sealed record GridBrushes(IBrush Surface, IBrush Raised, IBrush Text, IBrush HeaderText, IBrush Muted, Pen Border, Pen BorderSubtle, IBrush Selection, IBrush AccentSoft, IBrush SuccessSoft, IBrush DangerSoft, Pen DangerPen);
}
