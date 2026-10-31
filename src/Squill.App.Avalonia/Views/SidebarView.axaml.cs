using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Squill.Core;
using Squill.Presentation.Connections;
using Squill.Presentation.Panels;
using Squill.Presentation.Workspace;

namespace Squill.App.Avalonia.Views;

/// <summary>The connection list above the object explorer.</summary>
public sealed partial class SidebarView : UserControl
{
    private ConnectionListViewModel? _list;

    /// <summary>Creates the view.</summary>
    public SidebarView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_list is not null)
                _list.EditRequested -= OnEditRequested;
            _list = (DataContext as WorkspaceViewModel)?.ConnectionList;
            if (_list is not null)
                _list.EditRequested += OnEditRequested;
        };
    }

    /// <summary>Gives the history and snippets panels their view-models.</summary>
    /// <param name="history">The history view-model.</param>
    /// <param name="snippets">The snippets view-model.</param>
    public void Attach(HistoryViewModel history, SnippetsViewModel snippets)
    {
        HistoryPanel.DataContext = history;
        SnippetsPanel.DataContext = snippets;
    }

    private void OnPanelChecked(object? sender, RoutedEventArgs e)
    {
        ExplorerPanel.IsVisible = ExplorerTab.IsChecked == true;
        HistoryPanel.IsVisible = HistoryTab.IsChecked == true;
        SnippetsPanel.IsVisible = SnippetsTab.IsChecked == true;
        if (sender == HistoryTab && HistoryTab.IsChecked == true)
            (HistoryPanel.DataContext as HistoryViewModel)?.LoadCommand.Execute(null);
        else if (sender == SnippetsTab && SnippetsTab.IsChecked == true)
            (SnippetsPanel.DataContext as SnippetsViewModel)?.LoadCommand.Execute(null);
    }

    private async void OnEditRequested(object? sender, ConnectionEditorViewModel editor)
    {
        var window = new ConnectionEditorWindow { DataContext = editor };
        if (TopLevel.GetTopLevel(this) is Window owner)
            await window.ShowDialog(owner);
        else
            window.Show();
    }

    private void OnProfileDoubleTapped(object? sender, TappedEventArgs e) => Connect();

    private void OnProfileKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        Connect();
        e.Handled = true;
    }

    private void Connect()
    {
        if (DataContext is WorkspaceViewModel workspace && Profiles.SelectedItem is ConnectionProfile profile)
            workspace.ConnectCommand.Execute(profile);
    }
}
