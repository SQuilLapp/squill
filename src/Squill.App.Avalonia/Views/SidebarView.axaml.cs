using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Squill.Core;
using Squill.Presentation.Connections;
using Squill.Presentation.Panels;
using Squill.Presentation.Workspace;

namespace Squill.App.Avalonia.Views;

/// <summary>The panels of the activity rail.</summary>
public enum SidebarPanel
{
    /// <summary>Connections and the object explorer.</summary>
    Explorer,

    /// <summary>The query history.</summary>
    History,

    /// <summary>The saved snippets.</summary>
    Snippets,
}

/// <summary>The connection list above the object explorer, the history and the snippets, one at a time.</summary>
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
