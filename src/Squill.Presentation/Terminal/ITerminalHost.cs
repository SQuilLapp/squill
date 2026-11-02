namespace Squill.Presentation.Terminal;

/// <summary>A head-provided process host that runs a <see cref="TerminalLaunch"/> and exposes it to a terminal control.</summary>
public interface ITerminalHost
{
    /// <summary>Raised when the hosted process exits, possibly on a background thread.</summary>
    event EventHandler? Exited;

    /// <summary>Starts the process.</summary>
    /// <param name="launch">What to run.</param>
    void Start(TerminalLaunch launch);

    /// <summary>Terminates the process; terminating a host that is not running is a no-op.</summary>
    void Terminate();
}
