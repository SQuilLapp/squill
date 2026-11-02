using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Squill.Presentation.Terminal;

/// <summary>A shell or CLI tab: owns an <see cref="ITerminalHost"/> and tracks whether its process runs.</summary>
public sealed partial class TerminalTabViewModel : ViewModelBase, IDisposable
{
    private readonly TerminalLaunch _launch;

    /// <summary>Creates the tab without starting the process.</summary>
    /// <param name="host">Runs the process.</param>
    /// <param name="launch">What to run.</param>
    public TerminalTabViewModel(ITerminalHost host, TerminalLaunch launch)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(launch);
        Host = host;
        _launch = launch;
        Title = Path.GetFileNameWithoutExtension(launch.FileName);
        host.Exited += OnExited;
    }

    /// <summary>Gets the host the head attaches its terminal control to.</summary>
    public ITerminalHost Host { get; }

    /// <summary>Gets or sets the tab title.</summary>
    [ObservableProperty]
    public partial string Title { get; set; }

    /// <summary>Gets a value indicating whether the process is running.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    public partial bool IsRunning { get; private set; }

    [RelayCommand]
    private void Start()
    {
        if (IsRunning)
            Host.Terminate();
        Host.Start(_launch);
        IsRunning = true;
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Stop()
    {
        Host.Terminate();
        IsRunning = false;
    }

    private void OnExited(object? sender, EventArgs e) => IsRunning = false;

    /// <summary>Stops the process and detaches from the host.</summary>
    public void Dispose()
    {
        Host.Exited -= OnExited;
        Host.Terminate();
        IsRunning = false;
    }
}
