using Avalonia.Controls;
using Avalonia.Interactivity;
using Squill.Presentation.Connections;
using Squill.Presentation.Explorer;
using Squill.Presentation.Panels;
using Squill.Presentation.Workspace;

namespace Squill.App.Avalonia.Views;

/// <summary>The panels of the activity rail.</summary>
public enum SidebarPanel
{
    /// <summary>The connections and objects tree.</summary>
    Explorer,

    /// <summary>The query history.</summary>
    History,

    /// <summary>The saved snippets.</summary>
    Snippets,
}

/// <summary>The explorer tree of connections and objects, the history and the snippets, one at a time.</summary>
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

            ObjectExplorer.Workspace = DataContext as WorkspaceViewModel;
        };
        ObjectExplorer.Tree.SelectionChanged += (_, _) => SelectedNodeChanged?.Invoke(this, ObjectExplorer.Tree.SelectedItem as ExplorerNode);
    }

    /// <summary>Raised when the selection of the object tree changes; the argument is the selected node or <see langword="null"/>.</summary>
    public event EventHandler<ExplorerNode?>? SelectedNodeChanged;

    private void OnCollapseAllClicked(object? sender, RoutedEventArgs e)
    {
        foreach (var root in (DataContext as WorkspaceViewModel)?.Explorer.Roots ?? [])
            Collapse(root);
    }

    private static void Collapse(ExplorerNode node)
    {
        node.IsExpanded = false;
        foreach (var child in node.Children)
            Collapse(child);
    }

    /// <summary>Gives the history and snippets panels their view-models.</summary>
    /// <param name="history">The history view-model.</param>
    /// <param name="snippets">The snippets view-model.</param>
    public void Attach(HistoryViewModel history, SnippetsViewModel snippets)
    {
        HistoryPanel.DataContext = history;
        SnippetsPanel.DataContext = snippets;
    }

    /// <summary>Shows one of the left panels and loads its content, as the activity rail asks.</summary>
    /// <param name="panel">The panel to show.</param>
    public void ShowPanel(SidebarPanel panel)
    {
        ExplorerPanel.IsVisible = panel == SidebarPanel.Explorer;
        HistoryPanel.IsVisible = panel == SidebarPanel.History;
        SnippetsPanel.IsVisible = panel == SidebarPanel.Snippets;
        if (panel == SidebarPanel.History)
            (HistoryPanel.DataContext as HistoryViewModel)?.LoadCommand.Execute(null);
        else if (panel == SidebarPanel.Snippets)
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
}
