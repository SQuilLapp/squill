using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Squill.Platform.Storage;
using Squill.Presentation.Panels;

namespace Squill.App.WinUI.Views;

/// <summary>The query history list with its filter box.</summary>
public sealed partial class HistoryView : UserControl
{
    private HistoryViewModel? _history;

    /// <summary>Creates the view.</summary>
    public HistoryView() => InitializeComponent();

    /// <summary>Connects the view to the history it shows.</summary>
    /// <param name="history">The history view-model.</param>
    public void Attach(HistoryViewModel history)
    {
        _history = history;
        List.ItemsSource = history.Entries;
    }

    /// <summary>Reloads the entries from the store.</summary>
    public void Reload() => _history!.LoadCommand.Execute(null);

    private void OnFilterChanged(object sender, TextChangedEventArgs e)
    {
        if (_history is not null)
            _history.FilterText = FilterBox.Text;
    }

    private void OnClearClicked(object sender, RoutedEventArgs e) => _history!.ClearCommand.Execute(null);

    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => _history!.OpenCommand.Execute(List.SelectedItem as HistoryEntry);
}
