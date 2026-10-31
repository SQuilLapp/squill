using Squill.Presentation.Terminal;

namespace Squill.Presentation.Tests.Terminal;

/// <summary>Records the calls of a terminal tab and raises <c>Exited</c> on demand.</summary>
internal sealed class FakeTerminalHost : ITerminalHost
{
    public event EventHandler? Exited;

    public List<TerminalLaunch> Started { get; } = [];
    public int StopCount { get; private set; }

    public void Start(TerminalLaunch launch) => Started.Add(launch);

    public void Terminate() => StopCount++;

    public void RaiseExited() => Exited?.Invoke(this, EventArgs.Empty);
}
