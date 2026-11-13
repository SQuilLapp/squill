using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Squill.Presentation;
using Squill.Presentation.Editing;
using Squill.Presentation.Results;

namespace Squill.App.Avalonia.Controls;

/// <summary>Hosts a <see cref="DataGridCanvas"/> with its scroll bars and the text box that edits a cell in place.</summary>
public sealed class DataGridView : Grid
{
    private readonly DataGridCanvas _canvas = new();
    private readonly ScrollBar _vertical = new() { Orientation = Orientation.Vertical, AllowAutoHide = false };
    private readonly ScrollBar _horizontal = new() { Orientation = Orientation.Horizontal, AllowAutoHide = false };
    private readonly TextBox _editor = new() { IsVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Padding = new Thickness(6, 0), VerticalContentAlignment = VerticalAlignment.Center };
    private readonly Button _picker = new() { Content = "...", Focusable = false, Padding = new Thickness(6, 0), MinHeight = 0, VerticalAlignment = VerticalAlignment.Stretch, IsVisible = false };
    private GridEditController? _controller;
    private bool _syncing;

    /// <summary>Creates the view.</summary>
    public DataGridView()
    {
        RowDefinitions = new RowDefinitions("*,Auto");
        ColumnDefinitions = new ColumnDefinitions("*,Auto");
        SetColumn(_vertical, 1);
        SetRow(_horizontal, 1);
        Children.Add(_canvas);
        Children.Add(_vertical);
        Children.Add(_horizontal);
        Children.Add(_editor);
        _editor.Bind(TemplatedControl.BackgroundProperty, _editor.GetResourceObservable("SquillBgSurfaceBrush"));
        _editor.Bind(TemplatedControl.ForegroundProperty, _editor.GetResourceObservable("SquillFgBrush"));
        _editor.TextChanged += (_, _) =>
        {
            if (_controller is { IsEditing: true } c && !_syncing && c.EditText != (_editor.Text ?? ""))
            {
                c.SetText(_editor.Text ?? "");
            }
        };
        _editor.KeyDown += OnEditorKeyDown;
        AddHandler(PointerWheelChangedEvent, (_, e) =>
        {
            if (!e.Handled)
            {
                _canvas.ScrollByWheel(e);
            }
        });
        _editor.InnerRightContent = _picker;
        _picker.Click += (_, _) => OpenPicker();
        _canvas.ScrollStateChanged += (_, _) =>
        {
            SyncScrollBars();
            SyncEditor();
        };
        _vertical.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty && !_syncing)
            {
                _canvas.ScrollY = _vertical.Value;
            }
        };
        _horizontal.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty && !_syncing)
            {
                _canvas.ScrollX = _horizontal.Value;
            }
        };
    }

    /// <summary>Gets the drawing surface.</summary>
    public DataGridCanvas Canvas => _canvas;

    /// <summary>Gets or sets the data shown by the grid.</summary>
    public IGridDataSource? DataSource
    {
        get => _canvas.DataSource;
        set => _canvas.DataSource = value;
    }

    /// <summary>Gets or sets the selection the canvas draws; share the one given to <see cref="EditController"/>.</summary>
    public GridSelection Selection
    {
        get => _canvas.Selection;
        set => _canvas.Selection = value;
    }

    /// <summary>Gets or sets the cell-edit logic; <see langword="null"/> keeps the grid read-only.</summary>
    public GridEditController? EditController
    {
        get => _controller;
        set
        {
            if (_controller is not null)
            {
                _controller.EditStateChanged -= OnEditStateChanged;
            }

            _controller = value;
            _canvas.EditController = value;
            if (value is not null)
            {
                value.EditStateChanged += OnEditStateChanged;
            }

            SyncEditor();
        }
    }

    /// <summary>Gets or sets the service that reports paste errors.</summary>
    public IDialogService? Dialogs
    {
        get => _canvas.Dialogs;
        set => _canvas.Dialogs = value;
    }

    /// <summary>Gets the text box overlaid on the cell being edited.</summary>
    public TextBox CellEditor => _editor;

    private void OnEditStateChanged(object? sender, EventArgs e) => SyncEditor();

    private void OpenPicker() => CellValuePicker.Show(_editor, _controller, () => _canvas.Focus());

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (_controller is not { IsEditing: true } c)
        {
            return;
        }

        var handled = true;
        switch (e.Key)
        {
            case Key.Enter:
                c.Commit(EditMove.Down);
                break;
            case Key.Tab:
                c.Commit(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? EditMove.Left : EditMove.Right);
                break;
            case Key.Escape:
                c.Cancel();
                break;
            case Key.Up or Key.Down when c.EditorKind is not (CellEditorKind.Text or CellEditorKind.Json or CellEditorKind.Time or CellEditorKind.DateTime):
                c.Step(e.Key == Key.Up ? 1 : -1);
                break;
            case Key.Space when c.EditorKind == CellEditorKind.Boolean:
                c.Step(1);
                break;
            default:
                handled = false;
                break;
        }

        if (handled)
        {
            e.Handled = true;
            if (!c.IsEditing)
            {
                _canvas.Focus();
                _canvas.InvalidateVisual();
            }
        }
    }

    private void SyncEditor()
    {
        _syncing = true;
        try
        {
            var c = _controller;
            var rect = c is { IsEditing: true } ? _canvas.CellRect(c.EditRow, c.EditColumn) : null;
            if (c is not { IsEditing: true })
            {
                var had = _editor.IsVisible;
                _editor.IsVisible = false;
                if (had)
                {
                    _canvas.Focus();
                    _canvas.InvalidateVisual();
                }

                return;
            }

            if ((_editor.Text ?? "") != c.EditText)
            {
                _editor.Text = c.EditText;
                _editor.CaretIndex = _editor.Text.Length;
            }

            _editor.PlaceholderText = c.EditHint;
            _picker.IsVisible = CellEditorKinds.HasPicker(c.EditorKind);

            var danger = c.Error is not null;
            _editor.Bind(TemplatedControl.BorderBrushProperty, _editor.GetResourceObservable(danger ? "SquillDangerBrush" : "SquillAccentBrush"));
            _editor.BorderThickness = new Thickness(2);
            ToolTip.SetTip(_editor, c.Error);
            if (rect is { } r)
            {
                _editor.Margin = new Thickness(r.X, r.Y, 0, 0);
                _editor.Width = r.Width;
                _editor.Height = r.Height;
                var show = !_editor.IsVisible;
                _editor.IsVisible = true;
                if (show)
                {
                    _editor.Focus();
                    _editor.CaretIndex = _editor.Text?.Length ?? 0;
                    if (c.EditorKind == CellEditorKind.Boolean)
                        Dispatcher.UIThread.Post(OpenPicker);
                }
            }
            else
            {
                _editor.IsVisible = false;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SyncScrollBars()
    {
        _syncing = true;
        try
        {
            _vertical.Maximum = _canvas.MaxScrollY;
            _vertical.ViewportSize = _canvas.BodyHeight;
            _vertical.LargeChange = _canvas.BodyHeight;
            _vertical.SmallChange = _canvas.Layout.RowHeight;
            _vertical.Value = _canvas.ScrollY;
            _horizontal.Maximum = _canvas.MaxScrollX;
            _horizontal.ViewportSize = _canvas.Bounds.Width;
            _horizontal.LargeChange = _canvas.Bounds.Width;
            _horizontal.Value = _canvas.ScrollX;
        }
        finally
        {
            _syncing = false;
        }
    }
}
