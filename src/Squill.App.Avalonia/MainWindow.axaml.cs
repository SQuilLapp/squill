using Avalonia.Controls;
using Avalonia.Interactivity;
using Squill.Presentation.Query;
using Squill.Presentation.Workspace;

namespace Squill.App.Avalonia;

/// <summary>The workspace shell: sidebar, query tabs, editor with results, and status bar.</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Creates the window.</summary>
    public MainWindow() => InitializeComponent();

    private void OnCloseTabClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkspaceViewModel workspace && (sender as Control)?.DataContext is QueryTabViewModel tab)
            workspace.CloseTabCommand.Execute(tab);
    }
}
