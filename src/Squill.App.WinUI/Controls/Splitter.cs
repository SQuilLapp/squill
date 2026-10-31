using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Squill.App.WinUI.Controls;

/// <summary>
/// A thin draggable bar that resizes the row or column it sits next to; the one before it (<see cref="ResizeBefore"/>)
/// or after it receives the fixed size, the other one keeps filling the space.
/// </summary>
public sealed partial class Splitter : UserControl
{
    private const double Thickness = 4;
    private const double MinSize = 80;

    private readonly bool _vertical;
    private double _startSize;
    private double _startPointer;

    /// <summary>Creates a splitter.</summary>
    /// <param name="vertical"><see langword="true"/> for a bar between two columns, <see langword="false"/> between two rows.</param>
    public Splitter(bool vertical)
    {
        _vertical = vertical;
        Content = new Border { Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SquillBorderBrush"] };
        if (vertical)
            Width = Thickness;
        else
            Height = Thickness;
        ProtectedCursor = InputSystemCursor.Create(vertical ? InputSystemCursorShape.SizeWestEast : InputSystemCursorShape.SizeNorthSouth);
        PointerPressed += OnPressed;
        PointerMoved += OnMoved;
        PointerReleased += (_, e) => ReleasePointerCapture(e.Pointer);
    }

    /// <summary>Gets or sets whether the element before the bar (rather than after it) receives the fixed size.</summary>
    public bool ResizeBefore { get; set; } = true;

    private double Position(PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint((UIElement)Parent).Position;
        return _vertical ? p.X : p.Y;
    }

    private Grid ParentGrid => (Grid)Parent;

    private (RowDefinition? Row, ColumnDefinition? Column) Target()
    {
        var index = _vertical ? Grid.GetColumn(this) : Grid.GetRow(this);
        index += ResizeBefore ? -1 : 1;
        return _vertical ? (null, ParentGrid.ColumnDefinitions[index]) : (ParentGrid.RowDefinitions[index], null);
    }

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        var (row, column) = Target();
        _startSize = _vertical ? column!.ActualWidth : row!.ActualHeight;
        _startPointer = Position(e);
        CapturePointer(e.Pointer);
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        var delta = Position(e) - _startPointer;
        var size = Math.Max(MinSize, _startSize + (ResizeBefore ? delta : -delta));
        var (row, column) = Target();
        if (column is not null)
            column.Width = new GridLength(size);
        else
            row!.Height = new GridLength(size);
    }
}
