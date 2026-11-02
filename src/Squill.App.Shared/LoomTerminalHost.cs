#if SQUILL_LOOMTERMINAL
using LoomTerminal.Presentation.Session;
using LoomTerminal.Pty;
using Squill.Presentation.Terminal;

namespace Squill.App;

/// <summary>An <see cref="ITerminalHost"/> that runs the process behind LoomTerminal's pseudo terminal; compiled into both heads.</summary>
internal sealed class LoomTerminalHost : ITerminalHost
{
    /// <inheritdoc />
    public event EventHandler? Exited;

    /// <summary>Raised on the calling thread of <see cref="Start"/> and <see cref="Terminate"/> after <see cref="Session"/> changed.</summary>
    public event EventHandler? SessionChanged;

    /// <summary>Gets the running session a terminal control attaches to, or <see langword="null"/> when no process runs.</summary>
    public TerminalSession? Session { get; private set; }

    /// <inheritdoc />
    public void Start(TerminalLaunch launch)
    {
        ArgumentNullException.ThrowIfNull(launch);
        Terminate();
        var options = new PtyStartOptions(TerminalCommandLine.Build(launch))
        {
            WorkingDirectory = launch.WorkingDirectory,
            Environment = launch.Environment.ToDictionary(pair => pair.Key, pair => (string?)pair.Value),
        };
        var session = TerminalSession.Start(options, new TerminalSessionOptions { Scrollback = 10_000 });
        session.Exited += _ => Exited?.Invoke(this, EventArgs.Empty);
        Session = session;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Terminate()
    {
        if (Session is not { } session)
            return;
        Session = null;
        SessionChanged?.Invoke(this, EventArgs.Empty);
        session.Dispose();
    }
}
#endif
