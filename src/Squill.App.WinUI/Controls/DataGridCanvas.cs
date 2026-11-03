using System.Diagnostics;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Squill.Core;
using Squill.Presentation;
using Squill.Presentation.Editing;
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
    private const long DoubleClickMs = 500;
    private const float StateBarWidth = 3;
    private const float KeyGlyphWidth = 14;

    /// <summary>Width of the row-number column drawn left of the data columns.</summary>
    internal const double GutterWidth = 36;

    private readonly CanvasControl _canvas = new();
    private readonly LruCache<(string Text, Face Face, bool Struck), CanvasTextLayout> _text = new(4096, layout => layout.Dispose());
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
    private long _lastPressTick;
    private Point _lastPressPoint;
    private GridEditController? _edit;
    private Faces? _faces;

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
        _canvas.CharacterReceived += OnCharacterReceived;
        _canvas.ActualThemeChanged += (_, _) =>
        {
            _colors = null;
            _faces = null;
            _text.Clear();
            _needsMeasure = true;
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

    /// <summary>Gets or sets the selection; share the one given to <see cref="EditController"/> when editing.</summary>
    public GridSelection Selection { get; set; } = new();

    /// <summary>Gets or sets the cell-edit logic; <see langword="null"/> keeps the grid read-only.</summary>
    public GridEditController? EditController
    {
        get => _edit;
        set
        {
            _edit = value;
            _canvas.Invalidate();
        }
    }

    /// <summary>Gets or sets the service that reports paste errors.</summary>
    public IDialogService? Dialogs { get; set; }

    /// <summary>Gets the rectangle of a cell in surface coordinates, or <see langword="null"/> when it is scrolled out of the body.</summary>
    /// <param name="row">Grid row index.</param>
    /// <param name="column">Column index.</param>
    /// <returns>The visible cell rectangle.</returns>
    internal CellBox? CellRect(long row, int column) =>
        GridEditGeometry.EditorBox(_layout, row, column, _scrollX, _scrollY, _canvas.ActualWidth, BodyHeight, GutterWidth);

    /// <summary>Gives keyboard focus back to the drawing surface.</summary>
    public void FocusSurface() => _canvas.Focus(FocusState.Programmatic);

    /// <summary>Redraws the surface.</summary>
    public void Redraw() => _canvas.Invalidate();

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
    public double MaxScrollX => _layout.MaxScrollX(ViewportWidth);

    /// <summary>Gets the viewport width of the data columns (the surface minus the row-number column).</summary>
    public double ViewportWidth => Math.Max(0, _canvas.ActualWidth - GutterWidth);

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
            var (firstCol, endCol) = _layout.VisibleColumns(_scrollX, ViewportWidth);
            var (firstRow, endRow) = _layout.VisibleRows(_scrollY, BodyHeight, source.RowCount);
            for (var col = firstCol; col < endCol; col++)
            {
                DrawColumn(ds, source, c, col, (firstRow, endRow), height);
            }

            for (var row = firstRow; row < endRow; row++)
            {
                var y = (float)(_layout.HeaderHeight + ((row + 1) * _layout.RowHeight) - _scrollY - 0.5);
                ds.DrawLine((float)GutterWidth, y, width, y, c.BorderSubtle);
            }

            DrawGutter(ds, c, firstRow, endRow, height);
        }
        else
        {
            ds.FillRectangle(0, 0, width, (float)_layout.HeaderHeight, c.Raised);
        }

        var headerLine = (float)(_layout.HeaderHeight - 0.5);
        ds.DrawLine(0, headerLine, width, headerLine, c.Border);
        LastRenderMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        Drawn?.Invoke(this, EventArgs.Empty);
    }

    private void DrawColumn(CanvasDrawingSession ds, IGridDataSource source, GridColors c, int col, (long First, long End) rows, float height)
    {
        var left = (float)(GutterWidth + _layout.ColumnLeft(col) - _scrollX);
        var colWidth = (float)_layout.ColumnWidth(col);
        var headerHeight = (float)_layout.HeaderHeight;
        var rowHeight = (float)_layout.RowHeight;
        var info = source.Columns[col];
        var edit = _edit;
        using (ds.CreateLayer(1f, new Rect(left, 0, colWidth, height)))
        {
            for (var row = rows.First; row < rows.End; row++)
            {
                var top = (float)(headerHeight + (row * rowHeight) - _scrollY);
                var state = edit?.GetRowState(row) ?? RowState.Unchanged;
                if (state == RowState.Inserted)
                {
                    ds.FillRectangle(left, top, colWidth, rowHeight, c.CellAdded);
                }
                else if (state == RowState.Deleted)
                {
                    ds.FillRectangle(left, top, colWidth, rowHeight, c.CellDeleted);
                }
                else if (edit is not null && GridEditGeometry.ModifiedCellTint(state, edit.IsCellModified(row, col)))
                {
                    ds.FillRectangle(left, top, colWidth, rowHeight, c.CellModified);
                }

                if (Selection.Contains(row, col, source.RowCount))
                {
                    ds.FillRectangle(left, top, colWidth, rowHeight, c.Selection);
                }

                var value = CellFormatter.FormatDisplay(source.GetCell(row, col), info);
                var placeholder = edit?.GetPlaceholder(row, col);
                var face = Face.Mono;
                var color = c.Text;
                if (placeholder is not null)
                {
                    value = placeholder;
                    face = Face.MonoItalic;
                    color = c.Muted;
                }
                else if (value is null)
                {
                    value = "NULL";
                    face = Face.MonoItalic;
                    color = c.CellNull;
                }
                else if (edit is not null && !edit.IsColumnEditable(col))
                {
                    color = c.Muted;
                }

                var layout = TextLayout(ds, value, face, state == RowState.Deleted);
                var textWidth = (float)layout.LayoutBounds.Width;
                var x = IsNumeric(info) && face == Face.Mono ? Math.Max(left + CellPadding, left + colWidth - CellPadding - textWidth) : left + CellPadding;
                ds.DrawTextLayout(layout, x, top + ((rowHeight - (float)layout.LayoutBounds.Height) / 2), color);
                if (edit?.IsCellInvalid(row, col) == true)
                {
                    ds.DrawRectangle(left + 0.5f, top + 0.5f, colWidth - 1, rowHeight - 1, c.Danger, 1.5f);
                }
            }

            ds.FillRectangle(left, 0, colWidth, headerHeight, c.Raised);
            if (Selection.ContainsColumn(col))
            {
                ds.FillRectangle(left, 0, colWidth, headerHeight, c.Selection);
            }

            var headerX = left + CellPadding;
            if (edit?.IsKeyColumn(col) == true)
            {
                var cy = headerHeight / 2;
                using var diamond = CanvasGeometry.CreatePolygon(ds, [new Vector2(headerX + 4, cy - 4), new Vector2(headerX + 8, cy), new Vector2(headerX + 4, cy + 4), new Vector2(headerX, cy)]);
                ds.FillGeometry(diamond, c.Accent);
                headerX += KeyGlyphWidth;
            }

            var name = TextLayout(ds, info.Name, Face.UiSemibold);
            ds.DrawTextLayout(name, headerX, (headerHeight - (float)name.LayoutBounds.Height) / 2, c.HeaderText);
            var type = TextLayout(ds, info.DataTypeName, Face.MonoSmall);
            ds.DrawTextLayout(type, (float)GridEditGeometry.HeaderTypeX(headerX, name.LayoutBounds.Width), (headerHeight - (float)type.LayoutBounds.Height) / 2, c.Muted);
        }

        ds.DrawLine(left + colWidth - 0.5f, 0, left + colWidth - 0.5f, height, c.BorderSubtle);
    }

    private void DrawGutter(CanvasDrawingSession ds, GridColors c, long firstRow, long endRow, float height)
    {
        var gutter = (float)GutterWidth;
        var rowHeight = (float)_layout.RowHeight;
        var headerHeight = (float)_layout.HeaderHeight;
        ds.FillRectangle(0, 0, gutter, height, c.Raised);
        for (var row = firstRow; row < endRow; row++)
        {
            var top = (float)(headerHeight + (row * rowHeight) - _scrollY);
            var state = _edit?.GetRowState(row) ?? RowState.Unchanged;
            var color = state switch { RowState.Inserted => c.Success, RowState.Deleted => c.Danger, _ => c.Muted };
            var text = TextLayout(ds, GridEditGeometry.GutterLabel(state, row), Face.MonoSmall);
            ds.DrawTextLayout(text, gutter - CellPadding - (float)text.LayoutBounds.Width, top + ((rowHeight - (float)text.LayoutBounds.Height) / 2), color);
            if (GridEditGeometry.StateBar(state, top, rowHeight, StateBarWidth) is { } bar)
            {
                ds.FillRectangle((float)bar.X, (float)bar.Y, (float)bar.Width, (float)bar.Height, state == RowState.Inserted ? c.Success : c.Danger);
            }

            ds.DrawLine(0, top + rowHeight - 0.5f, gutter, top + rowHeight - 0.5f, c.BorderSubtle);
        }

        ds.FillRectangle(0, 0, gutter, headerHeight, c.Raised);
        ds.DrawLine(gutter - 0.5f, 0, gutter - 0.5f, height, c.BorderSubtle);
    }

    private static bool IsNumeric(ColumnInfo info) =>
        Type.GetTypeCode(Nullable.GetUnderlyingType(info.ClrType) ?? info.ClrType) is >= TypeCode.SByte and <= TypeCode.Decimal;

    private CanvasTextLayout TextLayout(ICanvasResourceCreator creator, string text, Face face, bool struck = false)
    {
        if (!_text.TryGet((text, face, struck), out var layout))
        {
            var faces = _faces ??= ResolveFaces();
            // The layout copies the format, so a short-lived one is enough.
            using var format = new CanvasTextFormat { WordWrapping = CanvasWordWrapping.NoWrap };
            switch (face)
            {
                case Face.UiSemibold:
                    format.FontFamily = faces.Ui;
                    format.FontSize = 13;
                    format.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
                    break;
                case Face.MonoSmall:
                    format.FontFamily = faces.Mono;
                    format.FontSize = 11;
                    break;
                case Face.MonoItalic:
                    format.FontFamily = faces.MonoItalic;
                    format.FontSize = faces.MonoSize;
                    format.FontStyle = Windows.UI.Text.FontStyle.Italic;
                    break;
                default:
                    format.FontFamily = faces.Mono;
                    format.FontSize = faces.MonoSize;
                    break;
            }

            layout = new CanvasTextLayout(creator, text, format, Unbounded, (float)_layout.RowHeight);
            if (struck)
            {
                layout.SetStrikethrough(0, text.Length, true);
            }

            _text.Add((text, face, struck), layout);
        }

        return layout;
    }

    private static Faces ResolveFaces()
    {
        // Win2D takes one family, so keep the first entry of each token's fallback list (the bundled file for mono).
        static string First(string key)
        {
            var family = ((Microsoft.UI.Xaml.Media.FontFamily)FindMerged(Application.Current.Resources, key)!).Source.Split(',')[0].Trim();
            // Win2D resolves ms-appx URIs only for packaged apps; the files sit next to the executable.
            const string AppX = "ms-appx:///";
            if (!family.StartsWith(AppX, StringComparison.Ordinal))
            {
                return family;
            }

            var parts = family[AppX.Length..].Split('#');
            return new Uri(Path.Combine(AppContext.BaseDirectory, parts[0])).AbsoluteUri + "#" + parts[1];
        }

        var mono = First("SquillFontMono");
        var size = FindMerged(Application.Current.Resources, "SquillFontSizeMonoGrid") is double d ? (float)d : 12;
        return new Faces(First("SquillFontUi"), mono, mono.Replace("-Regular.ttf", "-Italic.ttf", StringComparison.Ordinal), size);
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
            var info = source.Columns[col];
            var max = TextLayout(creator, info.Name, Face.UiSemibold).LayoutBounds.Width + 6 + TextLayout(creator, info.DataTypeName, Face.MonoSmall).LayoutBounds.Width + KeyGlyphWidth;
            for (var row = 0; row < sample; row++)
            {
                var value = CellFormatter.FormatDisplay(source.GetCell(row, col), info) ?? "NULL";
                max = Math.Max(max, TextLayout(creator, value, Face.Mono).LayoutBounds.Width);
            }

            widths[col] = max + (2 * CellPadding);
        }

        _layout.SetColumnWidths(widths);
        SetScroll(_scrollX, _scrollY);
    }

    private void OnSourceChanged(object? sender, EventArgs e)
    {
        // Rows still streaming in: re-measure on the next draw until the sample is full, unless the user chose the widths.
        // A pending measure must survive, or a source swapped in with more than SampleRows rows keeps zero columns.
        _needsMeasure = _layout.NeedsMeasure(_needsMeasure, _source?.RowCount ?? 0, SampleRows, _userResized);
        SetScroll(_scrollX, _scrollY);
        _canvas.Invalidate();
    }

    private void SetScroll(double x, double y)
    {
        var newX = Math.Clamp(x, 0, MaxScrollX);
        var newY = Math.Clamp(y, 0, MaxScrollY);
        if (Math.Abs(newX - _scrollX) > 0.001 || Math.Abs(newY - _scrollY) > 0.001)
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
        var column = point.Y <= _layout.HeaderHeight ? _layout.ResizeHandleAt(point.X - GutterWidth, _scrollX, ResizeTolerance) : -1;
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
        _edit?.Cancel();
        var shift = e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift);
        if (point.Y <= _layout.HeaderHeight)
        {
            var clicked = point.X < GutterWidth ? -1 : _layout.ColumnAt(point.X - GutterWidth + _scrollX);
            if (clicked >= 0)
            {
                Selection.SelectColumn(clicked, shift);
                _canvas.Invalidate();
            }
        }
        else
        {
            SelectAt(point, shift);
            var now = Environment.TickCount64;
            var doubleClick = now - _lastPressTick <= DoubleClickMs && Math.Abs(point.X - _lastPressPoint.X) <= ResizeTolerance && Math.Abs(point.Y - _lastPressPoint.Y) <= ResizeTolerance;
            _lastPressTick = doubleClick ? 0 : now;
            _lastPressPoint = point;
            if (doubleClick && _edit?.Begin() == true)
            {
                EnsureVisible(Selection.ActiveRow, Selection.ActiveColumn);
            }

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

        var onHandle = point.Y <= _layout.HeaderHeight && _layout.ResizeHandleAt(point.X - GutterWidth, _scrollX, ResizeTolerance) >= 0;
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
        else if (_edit is { } edit && ApplyEditKey(edit, e.Key, ctrl, shift))
        {
            // Handled below, like a navigation key.
        }
        else if (!ApplyKey(e.Key, ctrl, shift, source))
        {
            return;
        }

        e.Handled = true;
        EnsureVisible(Selection.ActiveRow, Selection.ActiveColumn);
        _canvas.Invalidate();
    }

    private void OnCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs e)
    {
        if (_edit is { } edit && !char.IsControl(e.Character) && edit.Begin(e.Character.ToString()))
        {
            e.Handled = true;
        }
    }

    private bool ApplyEditKey(GridEditController edit, VirtualKey key, bool ctrl, bool shift)
    {
        switch (key)
        {
            case VirtualKey.F2:
                return edit.Begin();
            case VirtualKey.Delete when ctrl:
                edit.DeleteSelectedRows();
                return true;
            case VirtualKey.Delete:
                edit.ClearSelection();
                return true;
            case VirtualKey.Insert when ctrl:
            case VirtualKey.N when ctrl && shift:
                edit.AddRow();
                return true;
            case VirtualKey.V when ctrl:
                _ = PasteAsync(edit);
                return true;
            default:
                return false;
        }
    }

    private async Task PasteAsync(GridEditController edit)
    {
        var content = Clipboard.GetContent();
        if (!content.Contains(StandardDataFormats.Text))
        {
            return;
        }

        var error = edit.Paste(await content.GetTextAsync());
        if (error is not null && Dialogs is { } dialogs)
        {
            await dialogs.ShowErrorAsync("Paste", error);
        }
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

        var column = _layout.ColumnAt(point.X - GutterWidth + _scrollX);
        if (column < 0)
        {
            column = point.X - GutterWidth + _scrollX < 0 ? 0 : source.Columns.Count - 1;
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
        var width = ViewportWidth;
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
            Get("SquillSelectionColor"),
            Get("SquillAccentColor"),
            Get("SquillSuccessColor"),
            Get("SquillDangerColor"),
            Get("SquillCellNullColor"),
            Get("SquillCellModifiedColor"),
            Get("SquillCellAddedColor"),
            Get("SquillCellDeletedColor"));
    }

    internal static object? FindMerged(ResourceDictionary dictionary, string key)
    {
        if (dictionary.TryGetValue(key, out var value))
        {
            return value;
        }

        return dictionary.MergedDictionaries.Select(merged => FindMerged(merged, key)).FirstOrDefault(found => found is not null);
    }

    internal static ResourceDictionary? FindThemeDictionary(ResourceDictionary dictionary, string key)
    {
        // Other merged dictionaries (the XAML controls resources) have their own "Default" and "Light" themes: match on one of our tokens.
        if (dictionary.ThemeDictionaries.TryGetValue(key, out var value) && value is ResourceDictionary theme && theme.ContainsKey("SquillBgSurfaceColor"))
        {
            return theme;
        }

        return dictionary.MergedDictionaries.Select(merged => FindThemeDictionary(merged, key)).FirstOrDefault(found => found is not null);
    }

    private enum Face
    {
        Mono,
        MonoItalic,
        MonoSmall,
        UiSemibold,
    }

    private sealed record Faces(string Ui, string Mono, string MonoItalic, float MonoSize);

    private sealed record GridColors(Color Surface, Color Raised, Color Text, Color HeaderText, Color Muted, Color Border, Color BorderSubtle, Color Selection, Color Accent, Color Success, Color Danger, Color CellNull, Color CellModified, Color CellAdded, Color CellDeleted);
}
