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

    private void OnTreeKeyDown(object? sender, KeyEventArgs e)
    {
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
        Tree.SelectedItem = node;
        var connection = node.Kind == ExplorerNodeKind.Connection;
        var table = node.Kind == ExplorerNodeKind.Table;
        var definition = HasDefinition(node.Kind);
        var shown = 0;
        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            item.IsVisible = item.Name switch
            {
                "NewItem" or "EditItem" or "DeleteItem" => connection,
                "ConnectItem" => connection && !node.IsConnected,
                "DisconnectItem" => connection && node.IsConnected,
                "DefinitionItem" => definition,
                _ => table,
            };
            shown += item.IsVisible ? 1 : 0;
        }
        if (shown == 0)
            e.Cancel = true;
    }

    private void OnNewClicked(object? sender, RoutedEventArgs e) => Workspace?.ConnectionList.NewCommand.Execute(null);

    private void OnEditClicked(object? sender, RoutedEventArgs e) => Workspace?.ConnectionList.EditCommand.Execute(null);

    private void OnDeleteClicked(object? sender, RoutedEventArgs e) => Workspace?.ConnectionList.DeleteCommand.Execute(null);

    private void OnConnectClicked(object? sender, RoutedEventArgs e) => Workspace?.ConnectCommand.Execute((sender as Control)?.DataContext is ExplorerNode node ? node.Profile : null);

    private void OnDisconnectClicked(object? sender, RoutedEventArgs e) => Workspace?.DisconnectCommand.Execute(null);

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
