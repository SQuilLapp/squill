using Avalonia.Controls;
using Avalonia.Input;
using Squill.Core;
using Squill.Presentation.Workspace;

namespace Squill.App.Avalonia.Views;

/// <summary>The connection list above the object explorer.</summary>
public sealed partial class SidebarView : UserControl
{
    /// <summary>Creates the view.</summary>
    public SidebarView() => InitializeComponent();

    private void OnProfileDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is WorkspaceViewModel workspace && Profiles.SelectedItem is ConnectionProfile profile)
            workspace.ConnectCommand.Execute(profile);
    }
}
