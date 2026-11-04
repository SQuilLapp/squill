using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Squill.Presentation.Connections;
using Squill.Presentation.Explorer;
using Squill.Presentation.Localization;
using Squill.Presentation.Panels;
using Squill.Presentation.Workspace;
using Windows.System;

namespace Squill.App.WinUI.Views;

/// <summary>The explorer tree of connections and objects, the history and the snippets, one at a time.</summary>
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
        Tree.ItemsSource = workspace.Explorer.Roots;
        OnProfilesChanged(null, null);
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

    private void OnProfilesChanged(object? sender, NotifyCollectionChangedEventArgs? e) =>
        EmptyHint.Visibility = List.Profiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void OnTreeKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter || Tree.SelectedItem is not ExplorerNode { Kind: ExplorerNodeKind.Connection } node)
            return;
        _workspace!.Explorer.ActivateCommand.Execute(node);
        e.Handled = true;
    }

    private void OnRefreshClicked(object sender, RoutedEventArgs e) => _workspace?.Explorer.RefreshCommand.Execute(null);

    private void OnCollapseAllClicked(object sender, RoutedEventArgs e)
    {
        foreach (var root in _workspace?.Explorer.Roots ?? [])
            Collapse(root);
    }

    private static void Collapse(ExplorerNode node)
    {
        node.IsExpanded = false;
        foreach (var child in node.Children)
            Collapse(child);
    }

    private void OnTreeSelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs e)
    {
        var node = e.AddedItems.OfType<ExplorerNode>().FirstOrDefault();
        if (node is not null)
            _workspace!.Explorer.SelectedNode = node;
        SelectedNodeChanged?.Invoke(this, node);
    }

    private void OnNewClicked(object sender, RoutedEventArgs e) => List.NewCommand.Execute(null);

    private void OnFilterChanged(object sender, TextChangedEventArgs e)
    {
        if (_workspace is not null)
            _workspace.Explorer.FilterText = Filter.Text;
    }

    private void OnTreeDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_workspace is null || Tree.SelectedItem is not ExplorerNode node)
            return;
        if (node.Kind is ExplorerNodeKind.View or ExplorerNodeKind.MaterializedView or ExplorerNodeKind.Function or ExplorerNodeKind.Procedure or ExplorerNodeKind.Trigger)
            _workspace.Explorer.OpenDefinitionCommand.Execute(node);
        else
            _workspace.Explorer.ActivateCommand.Execute(node);
    }

    // The flyout is built per request: connection nodes get the connection commands, tables Design/Drop, other nodes no menu.
    private void OnNodeContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        if (_workspace is null || (sender as FrameworkElement)?.DataContext is not ExplorerNode node)
            return;
        var explorer = _workspace.Explorer;
        var menu = new MenuFlyout();
        void Add(string text, Action action)
        {
            var item = new MenuFlyoutItem { Text = text };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }

        // A right click does not select the node, and the commands below act on the selection.
        if (node.Kind is ExplorerNodeKind.Connection or ExplorerNodeKind.Table)
            Tree.SelectedItem = node;
        if (node.Kind == ExplorerNodeKind.Connection)
        {
            Add(UiText.Get("explorer.newConnection"), () => List.NewCommand.Execute(null));
            Add(UiText.Get("explorer.edit"), () => List.EditCommand.Execute(null));
            Add(UiText.Get("explorer.delete"), () => List.DeleteCommand.Execute(null));
            if (node.IsConnected)
                Add(UiText.Get("explorer.disconnect"), () => _workspace.DisconnectCommand.Execute(null));
            else
                Add(UiText.Get("explorer.connect"), () => _workspace.ConnectCommand.Execute(node.Profile));
        }
        else if (node.Kind == ExplorerNodeKind.Table)
        {
            Add(UiText.Get("explorer.designTable"), () => explorer.DesignCommand.Execute(node));
            Add(UiText.Get("explorer.scriptCreateTable"), () => explorer.ScriptCreateTableCommand.Execute(node));
            Add(UiText.Get("explorer.dropTable"), () => explorer.DropCommand.Execute(node));
        }
        else if (node.Kind is ExplorerNodeKind.View or ExplorerNodeKind.MaterializedView or ExplorerNodeKind.Function or ExplorerNodeKind.Procedure or ExplorerNodeKind.Trigger)
        {
            Add(UiText.Get("explorer.openDefinition"), () => explorer.OpenDefinitionCommand.Execute(node));
        }
        else
        {
            return;
        }
        if (e.TryGetPosition(sender, out var position))
            menu.ShowAt(sender, new FlyoutShowOptions { Position = position });
        else
            menu.ShowAt((FrameworkElement)sender);
        e.Handled = true;
    }
}
