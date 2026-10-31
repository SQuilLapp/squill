using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Squill.Presentation.Results;

namespace Squill.App.WinUI.Controls;

/// <summary>Hosts a <see cref="DataGridCanvas"/> with its vertical and horizontal scroll bars.</summary>
public sealed partial class DataGridView : Grid
{
    private readonly DataGridCanvas _canvas = new();
    private readonly ScrollBar _vertical = new() { Orientation = Orientation.Vertical, IndicatorMode = ScrollingIndicatorMode.MouseIndicator };
    private readonly ScrollBar _horizontal = new() { Orientation = Orientation.Horizontal, IndicatorMode = ScrollingIndicatorMode.MouseIndicator };
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
        _canvas.ScrollStateChanged += (_, _) => SyncScrollBars();
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
