using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Squill.Presentation.Explorer;
using Squill.Presentation.Workspace;

namespace Squill.App.Avalonia.Views;

/// <summary>The tree of connections and their objects.</summary>
public sealed partial class ObjectExplorerView : UserControl
{
    /// <summary>Creates the view.</summary>
    public ObjectExplorerView() => InitializeComponent();

    /// <summary>Gets or sets the workspace whose connection commands the context menu runs.</summary>
    public WorkspaceViewModel? Workspace { get; set; }

    private ExplorerTreeViewModel? Explorer => DataContext as ExplorerTreeViewModel;

    private void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (Tree.SelectedItem is not ExplorerNode node)
            return;
        if (HasDefinition(node.Kind))
            Explorer?.OpenDefinitionCommand.Execute(node);
        else
            Explorer?.ActivateCommand.Execute(node);
    }

    private static bool HasDefinition(ExplorerNodeKind kind) =>
        kind is ExplorerNodeKind.View or ExplorerNodeKind.MaterializedView or ExplorerNodeKind.Function or ExplorerNodeKind.Procedure or ExplorerNodeKind.Trigger;

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        Explorer?.SetSelectedNodes(Tree.SelectedItems?.OfType<ExplorerNode>() ?? []);

    private void OnTreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && Explorer is { } explorer && explorer.DropSelectedTablesCommand.CanExecute(null))
        {
            explorer.DropSelectedTablesCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter || Tree.SelectedItem is not ExplorerNode { Kind: ExplorerNodeKind.Connection } node)
            return;
        Explorer?.ActivateCommand.Execute(node);
        e.Handled = true;
    }

    /// <summary>Runs the menu opening logic for <paramref name="menu"/>; the tests use it to check which entries show.</summary>
    /// <param name="menu">The menu whose data context is the node.</param>
    internal void OpenMenu(ContextMenu menu) => OnNodeMenuOpening(menu, new System.ComponentModel.CancelEventArgs());

    private void OnNodeMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // The menu inherits the node as its data context; a right click does not select it, so do that first.
        if (sender is not ContextMenu { DataContext: ExplorerNode node } menu)
            return;
        if (Tree.SelectedItems?.Contains(node) != true)
            Tree.SelectedItem = node;
        var connection = node.Kind == ExplorerNodeKind.Connection;
        var table = node.Kind == ExplorerNodeKind.Table;
        var listed = node.Kind is ExplorerNodeKind.Table or ExplorerNodeKind.View or ExplorerNodeKind.MaterializedView && node.Table is not null;
        var database = node is { Kind: ExplorerNodeKind.Database or ExplorerNodeKind.Schema, Session: not null };
        var definition = HasDefinition(node.Kind);
        var shown = 0;
        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            item.IsVisible = item.Name switch
            {
                "NewItem" or "EditItem" or "DeleteItem" => connection,
                "ConnectItem" => connection && !node.IsConnected,
                "DisconnectItem" => connection && node.IsConnected,
                "CompareItem" => connection && node.IsConnected,
                "MonitorItem" => connection && node.IsConnected,
                "SecurityItem" => connection && node.IsConnected,
                "ChooseShownItem" => Explorer?.CanFilter(node) ?? false,
                "ShowAllItem" => database && node.Kind == ExplorerNodeKind.Database && node.HiddenCount > 0,
                "HideTableItem" => listed,
                "RefreshItem" => node is { Kind: ExplorerNodeKind.Database, Session: not null },
                "DefinitionItem" => definition,
                _ => table,
            };
            if (item.Name == "SecurityItem" && item.IsVisible)
            {
                item.IsEnabled = Workspace?.OpenSecurityCommand.CanExecute(null) ?? false;
                ToolTip.SetTip(item, item.IsEnabled ? null : Workspace?.SecurityDisabledReason);
            }

            if (item.Name == "MonitorItem" && item.IsVisible)
            {
                item.IsEnabled = Workspace?.OpenSessionsMonitorCommand.CanExecute(null) ?? false;
                ToolTip.SetTip(item, item.IsEnabled ? null : Workspace?.SessionsMonitorDisabledReason);
            }

            if (item.Name == "CompareItem" && item.IsVisible)
            {
                item.IsEnabled = Workspace?.OpenSchemaCompareCommand.CanExecute(node.Profile?.Id) ?? false;
                ToolTip.SetTip(item, item.IsEnabled ? null : Workspace?.SchemaCompareDisabledReason);
            }

            if (item.Name == "DropItem")
            {
                item.Header = Explorer?.DropMenuText(node);
                item.IsEnabled = Explorer?.CanDropMenu(node) ?? true;
            }

            if (item.Name is "DesignItem" or "ScriptItem")
                item.IsEnabled = Explorer?.SelectedTableCount is not > 1 || !Explorer.SelectedTables.Contains(node);

            shown += item.IsVisible ? 1 : 0;
        }
        if (shown == 0)
            e.Cancel = true;
    }

    private void OnNewClicked(object? sender, RoutedEventArgs e) => Workspace?.ConnectionList.NewCommand.Execute(null);

    private void OnEditClicked(object? sender, RoutedEventArgs e) => Workspace?.ConnectionList.EditCommand.Execute(null);

    private void OnDeleteClicked(object? sender, RoutedEventArgs e) => Workspace?.ConnectionList.DeleteCommand.Execute(null);

    private void OnConnectClicked(object? sender, RoutedEventArgs e) => Workspace?.ConnectCommand.Execute((sender as Control)?.DataContext is ExplorerNode node ? node.Profile : null);

    private void OnCompareClicked(object? sender, RoutedEventArgs e) =>
        Workspace?.OpenSchemaCompareCommand.Execute((sender as Control)?.DataContext is ExplorerNode node ? node.Profile?.Id : null);

    private void OnSecurityClicked(object? sender, RoutedEventArgs e) => Workspace?.OpenSecurityCommand.Execute(null);

    private void OnMonitorClicked(object? sender, RoutedEventArgs e) => Workspace?.OpenSessionsMonitorCommand.Execute(null);

    private void OnDisconnectClicked(object? sender, RoutedEventArgs e) => Workspace?.DisconnectCommand.Execute(null);

    private void OnChooseShownClicked(object? sender, RoutedEventArgs e)
    {
        // Anchored to the tree: the explorer rebuilds the row nodes on every toggle.
        if ((sender as Control)?.DataContext is ExplorerNode node)
            ShowFilter(node, Tree);
    }

    private void OnBadgeClicked(object? sender, RoutedEventArgs e)
    {
        // Handled, so the click neither selects the row nor changes a multi-selection.
        e.Handled = true;
        if (sender is Button { DataContext: ExplorerNode node } badge)
            ShowFilter(node, badge);
    }

    private void ShowFilter(ExplorerNode node, Control anchor)
    {
        if (Explorer?.CreateFilter(node) is { } vm)
            ExplorerFilterMenu.Show(anchor, vm);
    }

    private void OnShowAllClicked(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is ExplorerNode node)
            Explorer?.ShowAllTablesCommand.Execute(node);
    }

    private void OnHideTableClicked(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is ExplorerNode node)
            Explorer?.HideTableCommand.Execute(node);
    }

    private void OnRefreshClicked(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is ExplorerNode node)
            Explorer?.RefreshDatabaseCommand.Execute(node);
    }

    private void OnDesignClicked(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is ExplorerNode node)
            Explorer?.DesignCommand.Execute(node);
    }

    private void OnDropClicked(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is ExplorerNode node)
            Explorer?.DropCommand.Execute(node);
    }

    private void OnDefinitionClicked(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is ExplorerNode node)
            Explorer?.OpenDefinitionCommand.Execute(node);
    }

    private void OnScriptCreateClicked(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is ExplorerNode node)
            Explorer?.ScriptCreateTableCommand.Execute(node);
    }
}
