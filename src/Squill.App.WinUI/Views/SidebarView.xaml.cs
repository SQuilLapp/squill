using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Squill.Core;
using Squill.Presentation.Connections;
using Squill.Presentation.Explorer;
using Squill.Presentation.Localization;
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
        TitleText.Text = UiText.Get("explorer.title");
        Filter.PlaceholderText = UiText.Get("explorer.filter");
        ToolTipService.SetToolTip(NewConnectionButton, UiText.Get("explorer.newConnection"));
        ToolTipService.SetToolTip(RefreshButton, UiText.Get("explorer.refresh"));
        ToolTipService.SetToolTip(CollapseButton, UiText.Get("explorer.collapseAll"));
    }

    /// <summary>Raised when the selection of the object tree changes; the argument is the selected node or <see langword="null"/>.</summary>
    public event EventHandler<ExplorerNode?>? SelectedNodeChanged;

    /// <summary>Gets the rows of the connection list: folder headers and connections.</summary>
    public IReadOnlyList<ConnectionRow> Rows { get; private set; } = [];

    /// <summary>Connects the view to the workspace, history and snippets it shows.</summary>
    /// <param name="workspace">The workspace view-model.</param>
    /// <param name="history">The history view-model.</param>
    /// <param name="snippets">The snippets view-model.</param>
    public void Attach(WorkspaceViewModel workspace, HistoryViewModel history, SnippetsViewModel snippets)
    {
        _workspace = workspace;
        HistoryPanel.Attach(history);
        SnippetsPanel.Attach(snippets);
        workspace.ConnectionList.Profiles.CollectionChanged += OnProfilesChanged;
        workspace.Sessions.CollectionChanged += OnProfilesChanged;
        workspace.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorkspaceViewModel.Explorer))
                ShowExplorer();
        };
        OnProfilesChanged(null, null);
        ShowExplorer();
    }

    /// <summary>Shows one of the left panels and loads its content, as the activity rail asks.</summary>
    /// <param name="panel">The panel to show.</param>
    public void ShowPanel(SidebarPanel panel)
    {
        ExplorerPanel.Visibility = ExplorerGlyphs.Show(panel == SidebarPanel.Explorer);
        HistoryPanel.Visibility = ExplorerGlyphs.Show(panel == SidebarPanel.History);
        SnippetsPanel.Visibility = ExplorerGlyphs.Show(panel == SidebarPanel.Snippets);
        if (panel == SidebarPanel.History)
            HistoryPanel.Reload();
        else if (panel == SidebarPanel.Snippets)
            SnippetsPanel.Reload();
    }

    private ConnectionListViewModel List => _workspace!.ConnectionList;

    private void OnProfilesChanged(object? sender, NotifyCollectionChangedEventArgs? e)
    {
        EmptyHint.Visibility = List.Profiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Rows = ConnectionRow.Build(List.Groups, _workspace!.Sessions.Select(s => s.Profile.Id).ToHashSet());
        Profiles.ItemsSource = Rows;
        Profiles.SelectedItem = Rows.FirstOrDefault(r => r.Profile is { } p && p.Id == List.SelectedProfile?.Id);
    }

    private void ShowExplorer()
    {
        Tree.ItemsSource = _workspace?.Explorer?.Roots;
        Filter.Text = "";
        SelectedNodeChanged?.Invoke(this, null);
    }

    private void OnProfileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Profiles.SelectedItem is ConnectionRow { Profile: { } profile })
            List.SelectedProfile = profile;
        else if (Profiles.SelectedItem is not null)
            Profiles.SelectedItem = null;
    }

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
        if (Profiles.SelectedItem is ConnectionRow { Profile: { } profile })
            _workspace!.ConnectCommand.Execute(profile);
    }

    private void OnRefreshClicked(object sender, RoutedEventArgs e) => _workspace?.Explorer?.RefreshCommand.Execute(null);

    private void OnCollapseAllClicked(object sender, RoutedEventArgs e)
    {
        foreach (var root in _workspace?.Explorer?.Roots ?? [])
            Collapse(root);
    }

    private static void Collapse(ExplorerNode node)
    {
        node.IsExpanded = false;
        foreach (var child in node.Children)
            Collapse(child);
    }

    private void OnTreeSelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs e) =>
        SelectedNodeChanged?.Invoke(this, e.AddedItems.OfType<ExplorerNode>().FirstOrDefault());

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

    // Only tables can be designed or dropped; the flyout is built per request so other nodes show no menu.
    private void OnNodeContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        if (_workspace?.Explorer is not { } explorer || (sender as FrameworkElement)?.DataContext is not ExplorerNode { Kind: ExplorerNodeKind.Table } node)
            return;
        var menu = new MenuFlyout();
        var design = new MenuFlyoutItem { Text = "Design table..." };
        design.Click += (_, _) => explorer.DesignCommand.Execute(node);
        var drop = new MenuFlyoutItem { Text = "Drop table..." };
        drop.Click += (_, _) => explorer.DropCommand.Execute(node);
        menu.Items.Add(design);
        menu.Items.Add(drop);
        if (e.TryGetPosition(sender, out var position))
            menu.ShowAt(sender, new FlyoutShowOptions { Position = position });
        else
            menu.ShowAt((FrameworkElement)sender);
        e.Handled = true;
    }
}
