using System.Diagnostics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Squill.Presentation.Results;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.System;
using Windows.UI;

namespace Squill.App.WinUI.Controls;

/// <summary>Custom-drawn grid surface on Win2D: paints only the visible cells and header, with no per-cell or per-row objects.</summary>
public sealed partial class DataGridCanvas : Grid
{
    private const int SampleRows = 100;
    private const float CellPadding = 8;
    private const double ResizeTolerance = 4;
    private const float Unbounded = 10_000;

    private readonly CanvasControl _canvas = new();
    private readonly LruCache<string, CanvasTextLayout> _text = new(4096, layout => layout.Dispose());
    private readonly GridLayout _layout;
    private IGridDataSource? _source;
    private GridColors? _colors;
    private double _scrollX;
    private double _scrollY;
    private bool _needsMeasure;
    private bool _userResized;
    private int _resizeColumn = -1;
    private double _resizeStartX;
    private double _resizeStartWidth;
    private bool _selecting;

    /// <summary>Creates the canvas, reading the row height from the token resources.</summary>
    public DataGridCanvas()
    {
        var rowHeight = FindMerged(Application.Current.Resources, "SquillRowHeightStandard") is double d ? d : 26;
        _layout = new GridLayout(rowHeight, rowHeight);
        Children.Add(_canvas);
        _canvas.Draw += OnDraw;
        _canvas.CreateResources += (_, _) => _text.Clear();
        _canvas.SizeChanged += (_, _) => SetScroll(_scrollX, _scrollY);
        _canvas.PointerWheelChanged += OnPointerWheelChanged;
        _canvas.PointerPressed += OnPointerPressed;
        _canvas.PointerMoved += OnPointerMoved;
        _canvas.PointerReleased += OnPointerReleased;
        _canvas.IsTabStop = true;
        _canvas.KeyDown += OnKeyDown;
        _canvas.ActualThemeChanged += (_, _) =>
        {
            _colors = null;
            _canvas.Invalidate();
        };
        _canvas.Unloaded += (_, _) =>
        {
            _text.Clear();
            _canvas.RemoveFromVisualTree();
        };
    }

    /// <summary>Raised when the content extent, viewport or scroll offsets changed so hosts can update scroll bars.</summary>
    public event EventHandler? ScrollStateChanged;

    /// <summary>Gets or sets the data shown by the grid.</summary>
    public IGridDataSource? DataSource
    {
        get => _source;
        set
        {
            if (_source is not null)
            {
                _source.Changed -= OnSourceChanged;
            }

            _source = value;
            if (_source is not null)
            {
                _source.Changed += OnSourceChanged;
            }

            Selection.Clear();
            _userResized = false;
            _needsMeasure = true;
            _layout.SetColumnWidths([]);
            SetScroll(0, 0);
            _canvas.Invalidate();
        }
    }

    /// <summary>Gets the current selection.</summary>
    public GridSelection Selection { get; } = new();

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

    /// <summary>Gets the duration of the last draw callback, in milliseconds.</summary>
    public double LastRenderMs { get; private set; }

    /// <summary>Gets the visible body height below the header.</summary>
    public double BodyHeight => Math.Max(0, _canvas.ActualHeight - _layout.HeaderHeight);

    /// <summary>Gets the highest valid vertical scroll offset.</summary>
    public double MaxScrollY => _layout.MaxScrollY(_source?.RowCount ?? 0, BodyHeight);

    /// <summary>Gets the highest valid horizontal scroll offset.</summary>
    public double MaxScrollX => _layout.MaxScrollX(_canvas.ActualWidth);

    /// <summary>Gets the viewport width.</summary>
    public double ViewportWidth => _canvas.ActualWidth;

    /// <summary>Raised after each draw callback finished.</summary>
    public event EventHandler? Drawn;

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var started = Stopwatch.GetTimestamp();
        var ds = args.DrawingSession;
        var c = _colors ??= ResolveColors();
        var width = (float)sender.ActualWidth;
        var height = (float)sender.ActualHeight;
        ds.Clear(c.Surface);
        if (_needsMeasure)
        {
            MeasureColumns(ds);
        }

        var source = _source;
        if (source is not null && _layout.ColumnCount > 0)
        {
            var (firstCol, endCol) = _layout.VisibleColumns(_scrollX, width);
            var (firstRow, endRow) = _layout.VisibleRows(_scrollY, BodyHeight, source.RowCount);
            for (var col = firstCol; col < endCol; col++)
            {
                DrawColumn(ds, source, c, col, (firstRow, endRow), height);
            }

            for (var row = firstRow; row < endRow; row++)
            {
                var y = (float)(_layout.HeaderHeight + ((row + 1) * _layout.RowHeight) - _scrollY - 0.5);
                ds.DrawLine(0, y, width, y, c.BorderSubtle);
            }
        }

        var headerLine = (float)(_layout.HeaderHeight - 0.5);
        ds.DrawLine(0, headerLine, width, headerLine, c.Border);
        LastRenderMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        Drawn?.Invoke(this, EventArgs.Empty);
    }

    private void DrawColumn(CanvasDrawingSession ds, IGridDataSource source, GridColors c, int col, (long First, long End) rows, float height)
    {
        var left = (float)(_layout.ColumnLeft(col) - _scrollX);
        var colWidth = (float)_layout.ColumnWidth(col);
        var headerHeight = (float)_layout.HeaderHeight;
        var rowHeight = (float)_layout.RowHeight;
        var info = source.Columns[col];
        using (ds.CreateLayer(1f, new Rect(left, 0, colWidth, height)))
        {
            for (var row = rows.First; row < rows.End; row++)
            {
                var top = (float)(headerHeight + (row * rowHeight) - _scrollY);
                var value = CellFormatter.Format(source.GetCell(row, col), info);
                if (Selection.Contains(row, col, source.RowCount))
                {
                    ds.FillRectangle(left, top, colWidth, rowHeight, c.Selection);
                }

                DrawText(ds, value ?? "NULL", left, top, rowHeight, value is null ? c.Muted : c.Text);
            }

            ds.FillRectangle(left, 0, colWidth, headerHeight, c.Raised);
            if (Selection.ContainsColumn(col))
            {
                ds.FillRectangle(left, 0, colWidth, headerHeight, c.Selection);
            }

            DrawText(ds, info.Name, left, 0, headerHeight, c.HeaderText);
        }

        ds.DrawLine(left + colWidth - 0.5f, 0, left + colWidth - 0.5f, height, c.Border);
    }

    private void DrawText(CanvasDrawingSession ds, string text, float left, float top, float rowHeight, Color color)
    {
        var layout = TextLayout(ds, text);
        ds.DrawTextLayout(layout, left + CellPadding, top + ((rowHeight - (float)layout.LayoutBounds.Height) / 2), color);
    }

    private CanvasTextLayout TextLayout(ICanvasResourceCreator creator, string text)
    {
        if (!_text.TryGet(text, out var layout))
        {
            // The layout copies the format, so a short-lived one is enough.
            using var format = new CanvasTextFormat { FontSize = 13, WordWrapping = CanvasWordWrapping.NoWrap };
            layout = new CanvasTextLayout(creator, text, format, Unbounded, (float)_layout.RowHeight);
            _text.Add(text, layout);
        }

        return layout;
    }

    private void MeasureColumns(ICanvasResourceCreator creator)
    {
        _needsMeasure = false;
        var source = _source;
        if (source is null)
        {
            return;
        }

        var sample = (int)Math.Min(SampleRows, source.RowCount);
        var widths = new double[source.Columns.Count];
        for (var col = 0; col < widths.Length; col++)
        {
            var max = TextLayout(creator, source.Columns[col].Name).LayoutBounds.Width;
            for (var row = 0; row < sample; row++)
            {
                var value = CellFormatter.Format(source.GetCell(row, col), source.Columns[col]) ?? "NULL";
                max = Math.Max(max, TextLayout(creator, value).LayoutBounds.Width);
            }

            widths[col] = max + (2 * CellPadding);
        }

        _layout.SetColumnWidths(widths);
        SetScroll(_scrollX, _scrollY);
    }

    private void OnSourceChanged(object? sender, EventArgs e)
    {
        // Rows still streaming in: re-measure on the next draw until the sample is full, unless the user chose the widths.
        _needsMeasure = !_userResized && (_source?.RowCount ?? 0) <= SampleRows;
        SetScroll(_scrollX, _scrollY);
        _canvas.Invalidate();
    }

    private void SetScroll(double x, double y)
    {
        var newX = Math.Clamp(x, 0, MaxScrollX);
        var newY = Math.Clamp(y, 0, MaxScrollY);
        if (!newX.Equals(_scrollX) || !newY.Equals(_scrollY))
        {
            _scrollX = newX;
            _scrollY = newY;
            _canvas.Invalidate();
        }

        ScrollStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var properties = e.GetCurrentPoint(_canvas).Properties;
        var delta = properties.MouseWheelDelta / 120.0 * _layout.RowHeight * 3;
        if (properties.IsHorizontalMouseWheel)
        {
            SetScroll(_scrollX + delta, _scrollY);
        }
        else if (e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift))
        {
            SetScroll(_scrollX - delta, _scrollY);
        }
        else
        {
            SetScroll(_scrollX, _scrollY - delta);
        }

        e.Handled = true;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_canvas).Position;
        var column = point.Y <= _layout.HeaderHeight ? _layout.ResizeHandleAt(point.X, _scrollX, ResizeTolerance) : -1;
        if (column >= 0)
        {
            _resizeColumn = column;
            _resizeStartX = point.X;
            _resizeStartWidth = _layout.ColumnWidth(column);
            _canvas.CapturePointer(e.Pointer);
            e.Handled = true;
            return;
        }

        _canvas.Focus(FocusState.Pointer);
        var shift = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift);
        if (point.Y <= _layout.HeaderHeight)
        {
            var clicked = _layout.ColumnAt(point.X + _scrollX);
            if (clicked >= 0)
            {
                Selection.SelectColumn(clicked, shift);
                _canvas.Invalidate();
            }
        }
        else
        {
            SelectAt(point, shift);
            _selecting = true;
            _canvas.CapturePointer(e.Pointer);
        }

        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_canvas).Position;
        if (_resizeColumn >= 0)
        {
            _layout.SetColumnWidth(_resizeColumn, _resizeStartWidth + point.X - _resizeStartX);
            _userResized = true;
            SetScroll(_scrollX, _scrollY);
            _canvas.Invalidate();
            return;
        }

        if (_selecting)
        {
            SelectAt(point, true);
            return;
        }

        var onHandle = point.Y <= _layout.HeaderHeight && _layout.ResizeHandleAt(point.X, _scrollX, ResizeTolerance) >= 0;
        ProtectedCursor = onHandle ? InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast) : null;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_resizeColumn >= 0 || _selecting)
        {
            _resizeColumn = -1;
            _selecting = false;
            _canvas.ReleasePointerCapture(e.Pointer);
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_source is not { } source)
        {
            return;
        }

        var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (ctrl && e.Key == VirtualKey.C)
        {
            if (Selection.HasSelection)
            {
                var package = new DataPackage();
                package.SetText(Selection.ToTsv(source));
                Clipboard.SetContent(package);
            }
        }
        else if (!ApplyKey(e.Key, ctrl, shift, source))
        {
            return;
        }

        e.Handled = true;
        EnsureVisible(Selection.ActiveRow, Selection.ActiveColumn);
        _canvas.Invalidate();
    }

    private bool ApplyKey(VirtualKey key, bool ctrl, bool shift, IGridDataSource source)
    {
        var rows = source.RowCount;
        var cols = source.Columns.Count;
        var page = Math.Max(1, (long)(BodyHeight / _layout.RowHeight) - 1);
        (long Rows, long Cols)? delta = key switch
        {
            VirtualKey.Up => (-1, 0),
            VirtualKey.Down => (1, 0),
            VirtualKey.Left => (0, -1),
            VirtualKey.Right => (0, 1),
            VirtualKey.PageUp => (-page, 0),
            VirtualKey.PageDown => (page, 0),
            VirtualKey.Home => (ctrl ? -rows : 0, -cols),
            VirtualKey.End => (ctrl ? rows : 0, cols),
            _ => null,
        };
        if (delta is { } d)
        {
            Selection.Move(d.Rows, d.Cols, rows, cols, shift);
            return true;
        }

        if (ctrl && key == VirtualKey.A)
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
        _canvas.Invalidate();
    }

    private void EnsureVisible(long row, int column)
    {
        var top = row * _layout.RowHeight;
        var y = top < _scrollY ? top : top + _layout.RowHeight > _scrollY + BodyHeight ? top + _layout.RowHeight - BodyHeight : _scrollY;
        var left = _layout.ColumnLeft(column);
        var right = left + _layout.ColumnWidth(column);
        var width = _canvas.ActualWidth;
        var x = left < _scrollX ? left : right > _scrollX + width ? right - width : _scrollX;
        SetScroll(x, y);
    }

    private GridColors ResolveColors()
    {
        var key = _canvas.ActualTheme == ElementTheme.Light ? "Light" : "Default";
        var theme = (ResourceDictionary)FindThemeDictionary(Application.Current.Resources, key)!;
        Color Get(string name) => (Color)theme[name];
        return new GridColors(
            Get("SquillBgSurfaceColor"),
            Get("SquillBgRaisedColor"),
            Get("SquillFgColor"),
            Get("SquillFg2Color"),
            Get("SquillFg3Color"),
            Get("SquillBorderColor"),
            Get("SquillBorderSubtleColor"),
            Get("SquillSelectionColor"));
    }

    private static object? FindMerged(ResourceDictionary dictionary, string key)
    {
        if (dictionary.TryGetValue(key, out var value))
        {
            return value;
        }

        return dictionary.MergedDictionaries.Select(merged => FindMerged(merged, key)).FirstOrDefault(found => found is not null);
    }

    private static ResourceDictionary? FindThemeDictionary(ResourceDictionary dictionary, string key)
    {
        // Other merged dictionaries (the XAML controls resources) have their own "Default" and "Light" themes: match on one of our tokens.
        if (dictionary.ThemeDictionaries.TryGetValue(key, out var value) && value is ResourceDictionary theme && theme.ContainsKey("SquillBgSurfaceColor"))
        {
            return theme;
        }

        return dictionary.MergedDictionaries.Select(merged => FindThemeDictionary(merged, key)).FirstOrDefault(found => found is not null);
    }

    private sealed record GridColors(Color Surface, Color Raised, Color Text, Color HeaderText, Color Muted, Color Border, Color BorderSubtle, Color Selection);
}
