using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Squill.Presentation.Results;

namespace Squill.App.Avalonia.Controls;

/// <summary>Hosts a <see cref="DataGridCanvas"/> with its vertical and horizontal scroll bars.</summary>
public sealed class DataGridView : Grid
{
    private readonly DataGridCanvas _canvas = new();
    private readonly ScrollBar _vertical = new() { Orientation = Orientation.Vertical, AllowAutoHide = false };
    private readonly ScrollBar _horizontal = new() { Orientation = Orientation.Horizontal, AllowAutoHide = false };
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
        _canvas.ScrollStateChanged += (_, _) => SyncScrollBars();
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
