using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Squill.App.WinUI.Views;
using Squill.Presentation.Workspace;
using Windows.System;

namespace Squill.App.WinUI;

/// <summary>The home page of the shell: shown while nothing is open, it hides the rail, the explorer and the tab area.</summary>
public sealed partial class MainWindow
{
    // "Manage connections" shows the explorer even though nothing is open; the next change of the home state ends it.
    private bool _homeDismissed;

    private void AttachHome(WorkspaceViewModel workspace, Task loading)
    {
        HomeArea.Attach(workspace.Home);
        workspace.Home.ManageConnectionsRequested += (_, _) =>
        {
            _homeDismissed = true;
            SyncHome();
            ShowRailPanel(SidebarPanel.Explorer);
        };
        workspace.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(WorkspaceViewModel.IsHomeVisible))
                return;
            _homeDismissed = false;
            SyncHome();
        };
        for (var i = 1; i <= HomeViewModelLimit; i++)
        {
            var position = i;
            var accelerator = new KeyboardAccelerator { Key = VirtualKey.Number0 + position, Modifiers = VirtualKeyModifiers.Control };
            accelerator.Invoked += (_, args) =>
            {
                if (HomeArea.Visibility == Visibility.Visible && HomeArea.OpenCard(position))
                    args.Handled = true;
            };
            Root.KeyboardAccelerators.Add(accelerator);
        }

        _ = StartHomeAsync(workspace, loading);
    }

    private const int HomeViewModelLimit = Squill.Presentation.Home.HomeViewModel.MaxCards;

    private async Task StartHomeAsync(WorkspaceViewModel workspace, Task loading)
    {
        await loading;
        await workspace.InitializeHomeAsync(CancellationToken.None);
        SyncHome();
    }

    private void SyncHome()
    {
        var home = Workspace.IsHomeVisible && !_homeDismissed;
        HomeArea.Visibility = home ? Visibility.Visible : Visibility.Collapsed;
        var rest = home ? Visibility.Collapsed : Visibility.Visible;
        ActivityRail.Visibility = rest;
        Sidebar.Visibility = rest;
        MainArea.Visibility = rest;
        _sidebarSplitter!.Visibility = rest;
    }
}
