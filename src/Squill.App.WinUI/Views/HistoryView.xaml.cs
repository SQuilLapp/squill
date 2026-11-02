using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Squill.Platform.Storage;
using Squill.Presentation.Localization;
using Squill.Presentation.Panels;

namespace Squill.App.WinUI.Views;

/// <summary>The query history list with its filter box.</summary>
public sealed partial class HistoryView : UserControl
{
    private HistoryViewModel? _history;

    /// <summary>Creates the view.</summary>
    public HistoryView()
    {
        InitializeComponent();
        FilterBox.PlaceholderText = UiText.Get("editor.historySearch");
        ClearButton.Content = UiText.Get("editor.historyClear");
    }

    /// <summary>Picks the dot color of an entry.</summary>
    /// <param name="succeeded">Whether the statement succeeded.</param>
    /// <returns>The success or danger brush.</returns>
    public static Microsoft.UI.Xaml.Media.Brush StatusBrush(bool succeeded) =>
        (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[succeeded ? "SquillSuccessBrush" : "SquillDangerBrush"];

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
