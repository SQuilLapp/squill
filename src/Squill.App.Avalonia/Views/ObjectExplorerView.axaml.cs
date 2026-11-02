using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Squill.Presentation.Explorer;

namespace Squill.App.Avalonia.Views;

/// <summary>The schema tree of the active session.</summary>
public sealed partial class ObjectExplorerView : UserControl
{
    /// <summary>Creates the view.</summary>
    public ObjectExplorerView() => InitializeComponent();

    private void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is ObjectExplorerViewModel explorer && Tree.SelectedItem is ExplorerNode node)
            explorer.ActivateCommand.Execute(node);
    }

    private void OnNodeMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Only tables can be designed or dropped; the menu inherits the node as its data context.
        if ((sender as ContextMenu)?.DataContext is not ExplorerNode { Kind: ExplorerNodeKind.Table })
            e.Cancel = true;
    }

    private void OnDesignClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ObjectExplorerViewModel explorer && (sender as Control)?.DataContext is ExplorerNode node)
            explorer.DesignCommand.Execute(node);
    }

    private void OnDropClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ObjectExplorerViewModel explorer && (sender as Control)?.DataContext is ExplorerNode node)
            explorer.DropCommand.Execute(node);
    }
}
