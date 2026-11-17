using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using Squill.Presentation.Home;
using Squill.Presentation.Workspace;

namespace Squill.App.Avalonia;

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
            RailExplorer.IsChecked = true;
        };
        workspace.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(WorkspaceViewModel.IsHomeVisible))
                return;
            _homeDismissed = false;
            SyncHome();
        };
        for (var i = 1; i <= HomeViewModel.MaxCards; i++)
        {
            var position = i;
            KeyBindings.Add(new KeyBinding
            {
                Gesture = new KeyGesture(Key.D0 + position, KeyModifiers.Control),
                Command = new RelayCommand(() =>
                {
                    if (HomeArea.IsVisible)
                        HomeArea.OpenCard(position);
                }),
            });
        }

        _ = StartHomeAsync(workspace, loading);
    }

    private async Task StartHomeAsync(WorkspaceViewModel workspace, Task loading)
    {
        await loading;
        await workspace.InitializeHomeAsync(CancellationToken.None);
        SyncHome();
    }

    private void SyncHome()
    {
        var home = DataContext is WorkspaceViewModel { IsHomeVisible: true } && !_homeDismissed;
        HomeArea.IsVisible = home;
        ActivityRail.IsVisible = !home;
        Sidebar.IsVisible = !home;
        SidebarSplitter.IsVisible = !home;
        MainArea.IsVisible = !home;
    }
}
