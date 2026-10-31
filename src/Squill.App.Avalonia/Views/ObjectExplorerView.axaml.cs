using Avalonia.Controls;
using Avalonia.Input;
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
}
