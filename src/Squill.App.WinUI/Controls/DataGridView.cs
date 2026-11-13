using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Squill.Presentation;
using Squill.Presentation.Editing;
using Squill.Presentation.Results;
using Windows.System;

namespace Squill.App.WinUI.Controls;

/// <summary>Hosts a <see cref="DataGridCanvas"/> with its scroll bars and the text box that edits a cell in place.</summary>
public sealed partial class DataGridView : Grid
{
    private readonly DataGridCanvas _canvas = new();
    private readonly ScrollBar _vertical = new() { Orientation = Orientation.Vertical, IndicatorMode = ScrollingIndicatorMode.MouseIndicator };
    private readonly ScrollBar _horizontal = new() { Orientation = Orientation.Horizontal, IndicatorMode = ScrollingIndicatorMode.MouseIndicator };
    private readonly Canvas _overlay = new();
    private readonly TextBox _editor = new() { BorderThickness = new Thickness(0), Padding = new Thickness(6, 0, 6, 0), VerticalContentAlignment = VerticalAlignment.Center, MinHeight = 0, MinWidth = 0 };
    private readonly Border _editorFrame = new() { Visibility = Visibility.Collapsed };
    private readonly Button _picker = new() { Content = "...", IsTabStop = false, Padding = new Thickness(6, 0, 6, 0), MinHeight = 0, MinWidth = 0, VerticalAlignment = VerticalAlignment.Stretch, Visibility = Visibility.Collapsed };
    private GridEditController? _controller;
    private bool _syncing;

    /// <summary>Creates the view.</summary>
    public DataGridView()
    {
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        SetColumn(_vertical, 1);
        SetRow(_horizontal, 1);
        Children.Add(_canvas);
        Children.Add(_vertical);
        Children.Add(_horizontal);
        var editorRow = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } };
        SetColumn(_picker, 1);
        editorRow.Children.Add(_editor);
        editorRow.Children.Add(_picker);
        _editorFrame.Child = editorRow;
        _picker.Click += (_, _) => OpenPicker();
        _overlay.Children.Add(_editorFrame);
        Children.Add(_overlay);
        _editor.TextChanged += (_, _) =>
        {
            if (_controller is { IsEditing: true } c && !_syncing && c.EditText != _editor.Text)
            {
                c.SetText(_editor.Text);
            }
        };
        _editor.KeyDown += OnEditorKeyDown;
        _canvas.ScrollStateChanged += (_, _) =>
        {
            SyncScrollBars();
            SyncEditor();
        };
        _vertical.ValueChanged += (_, e) => ApplyScroll(() => _canvas.ScrollY = e.NewValue);
        _horizontal.ValueChanged += (_, e) => ApplyScroll(() => _canvas.ScrollX = e.NewValue);
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

    private void OpenPicker()
    {
        if (_controller is not { IsEditing: true } c)
            return;
        CellValuePicker.Show(_editor, c.EditorKind, c.EditText, c.CanUseDefault, text =>
        {
            if (text is null)
                c.SetDefault();
            else
                c.SetText(text);
            if (c.EditorKind == CellEditorKind.Boolean && c.Commit(EditMove.None))
                _canvas.FocusSurface();
        });
    }

    private void OnEditorKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (_controller is not { IsEditing: true } c)
        {
            return;
        }

        var shift = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        switch (e.Key)
        {
            case VirtualKey.Enter:
                c.Commit(EditMove.Down);
                break;
            case VirtualKey.Tab:
                c.Commit(shift ? EditMove.Left : EditMove.Right);
                break;
            case VirtualKey.Escape:
                c.Cancel();
                break;
            case VirtualKey.Up or VirtualKey.Down when c.EditorKind is not (CellEditorKind.Text or CellEditorKind.Json or CellEditorKind.Time or CellEditorKind.DateTime):
                c.Step(e.Key == VirtualKey.Up ? 1 : -1);
                break;
            case VirtualKey.Space when c.EditorKind == CellEditorKind.Boolean:
                c.Step(1);
                break;
            default:
                return;
        }

        e.Handled = true;
        if (!c.IsEditing)
        {
            _canvas.FocusSurface();
            _canvas.Redraw();
        }
    }

    private void SyncEditor()
    {
        _syncing = true;
        try
        {
            if (_controller is not { IsEditing: true } c)
            {
                var had = _editorFrame.Visibility == Visibility.Visible;
                _editorFrame.Visibility = Visibility.Collapsed;
                if (had)
                {
                    _canvas.FocusSurface();
                    _canvas.Redraw();
                }

                return;
            }

            if (_editor.Text != c.EditText)
            {
                _editor.Text = c.EditText;
                _editor.SelectionStart = _editor.Text.Length;
            }

            _editor.PlaceholderText = c.EditHint ?? "";
            _picker.Visibility = CellEditorKinds.HasPicker(c.EditorKind) ? Visibility.Visible : Visibility.Collapsed;

            var danger = c.Error is not null;
            _editorFrame.BorderBrush = (Brush)Application.Current.Resources[danger ? "SquillDangerBrush" : "SquillAccentBrush"];
            _editorFrame.BorderThickness = new Thickness(danger ? 2 : 1);
            ToolTipService.SetToolTip(_editor, c.Error);
            if (_canvas.CellRect(c.EditRow, c.EditColumn) is { } r)
            {
                Microsoft.UI.Xaml.Controls.Canvas.SetLeft(_editorFrame, r.X);
                Microsoft.UI.Xaml.Controls.Canvas.SetTop(_editorFrame, r.Y);
                _editorFrame.Width = r.Width;
                _editorFrame.Height = r.Height;
                var show = _editorFrame.Visibility != Visibility.Visible;
                _editorFrame.Visibility = Visibility.Visible;
                if (show)
                {
                    // The box must be laid out before it can take focus.
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        _editor.Focus(FocusState.Programmatic);
                        _editor.SelectionStart = _editor.Text.Length;
                        if (c.EditorKind == CellEditorKind.Boolean)
                            OpenPicker();
                    });
                }
            }
            else
            {
                _editorFrame.Visibility = Visibility.Collapsed;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void ApplyScroll(Action apply)
    {
        if (!_syncing)
        {
            apply();
        }
    }

    private void SyncScrollBars()
    {
        _syncing = true;
        try
        {
            Sync(_vertical, _canvas.MaxScrollY, _canvas.BodyHeight, _canvas.Layout.RowHeight, _canvas.ScrollY);
            Sync(_horizontal, _canvas.MaxScrollX, _canvas.ViewportWidth, _canvas.Layout.RowHeight, _canvas.ScrollX);
        }
        finally
        {
            _syncing = false;
        }
    }

    private static void Sync(ScrollBar bar, double max, double viewport, double small, double value)
    {
        bar.Maximum = max;
        bar.ViewportSize = viewport;
        bar.LargeChange = viewport;
        bar.SmallChange = small;
        bar.Value = value;
    }
}
