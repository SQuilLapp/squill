using Avalonia.Controls;
using Avalonia.Input;
using Squill.Core;
using Squill.Presentation.Connections;
using Squill.Presentation.Workspace;

namespace Squill.App.Avalonia.Views;

/// <summary>The connection list above the object explorer.</summary>
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
