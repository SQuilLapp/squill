using Avalonia.Controls;
using Avalonia.Input;
using Squill.Platform.Storage;
using Squill.Presentation.Panels;

namespace Squill.App.Avalonia.Views;

/// <summary>The filterable query history; a double-click reopens an entry in a new tab.</summary>
public sealed partial class HistoryView : UserControl
{
    /// <summary>Creates the view.</summary>
    public HistoryView() => InitializeComponent();

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is HistoryViewModel history && List.SelectedItem is HistoryEntry entry)
            history.OpenCommand.Execute(entry);
    }
}
