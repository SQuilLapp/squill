using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Squill.Core;
using Squill.Presentation.Connections;
using Squill.Presentation.Explorer;
using Squill.Presentation.Panels;
using Squill.Presentation.Workspace;
using Windows.System;

namespace Squill.App.WinUI.Views;

/// <summary>The connection list above the object explorer.</summary>
public sealed partial class SidebarView : UserControl
{
    private WorkspaceViewModel? _workspace;

    /// <summary>Creates the view.</summary>
    public SidebarView()
    {
        InitializeComponent();
        // Checked here, not in XAML: the handler needs every panel, which the parser has not created yet while it sets the property.
        ExplorerTab.IsChecked = true;
    }

    /// <summary>Connects the view to the workspace, history and snippets it shows.</summary>
    /// <param name="workspace">The workspace view-model.</param>
    /// <param name="history">The history view-model.</param>
    /// <param name="snippets">The snippets view-model.</param>
    public void Attach(WorkspaceViewModel workspace, HistoryViewModel history, SnippetsViewModel snippets)
    {
        _workspace = workspace;
        HistoryPanel.Attach(history);
        SnippetsPanel.Attach(snippets);
        Profiles.ItemsSource = workspace.ConnectionList.Profiles;
        workspace.ConnectionList.Profiles.CollectionChanged += OnProfilesChanged;
        workspace.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorkspaceViewModel.Explorer))
                ShowExplorer();
        };
        OnProfilesChanged(null, null);
        ShowExplorer();
    }

    private void OnPanelChecked(object sender, RoutedEventArgs e)
    {
        ExplorerPanel.Visibility = ExplorerGlyphs.Show(ExplorerTab.IsChecked == true);
        HistoryPanel.Visibility = ExplorerGlyphs.Show(HistoryTab.IsChecked == true);
        SnippetsPanel.Visibility = ExplorerGlyphs.Show(SnippetsTab.IsChecked == true);
        if (ReferenceEquals(sender, HistoryTab))
            HistoryPanel.Reload();
        else if (ReferenceEquals(sender, SnippetsTab))
            SnippetsPanel.Reload();
    }

    private ConnectionListViewModel List => _workspace!.ConnectionList;

    private void OnProfilesChanged(object? sender, NotifyCollectionChangedEventArgs? e) =>
        EmptyHint.Visibility = List.Profiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void ShowExplorer()
    {
        Tree.ItemsSource = _workspace?.Explorer?.Roots;
        Filter.Text = "";
    }

    private void OnProfileSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        List.SelectedProfile = Profiles.SelectedItem as ConnectionProfile;

    private void OnProfileDoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => Connect();

    private void OnProfileKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
            return;
        Connect();
        e.Handled = true;
    }

    private void Connect()
    {
        if (Profiles.SelectedItem is ConnectionProfile profile)
            _workspace!.ConnectCommand.Execute(profile);
    }

    private void OnNewClicked(object sender, RoutedEventArgs e) => List.NewCommand.Execute(null);

    private void OnEditClicked(object sender, RoutedEventArgs e) => List.EditCommand.Execute(null);

    private void OnDeleteClicked(object sender, RoutedEventArgs e) => List.DeleteCommand.Execute(null);

    private void OnConnectClicked(object sender, RoutedEventArgs e) => Connect();

    private void OnDisconnectClicked(object sender, RoutedEventArgs e) => _workspace!.DisconnectCommand.Execute(null);

    private void OnFilterChanged(object sender, TextChangedEventArgs e)
    {
        if (_workspace?.Explorer is { } explorer)
            explorer.FilterText = Filter.Text;
    }

    private void OnTreeDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_workspace?.Explorer is { } explorer && Tree.SelectedItem is ExplorerNode node)
            explorer.ActivateCommand.Execute(node);
    }
}
