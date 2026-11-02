using Squill.Core;
using Squill.Presentation.Tests.Panels;
using Squill.Presentation.Terminal;
using Xunit;

namespace Squill.Presentation.Tests.Terminal;

public sealed class TerminalTabViewModelTests
{
    private static readonly TerminalLaunch Launch = new("/bin/sh", [], null, new Dictionary<string, string>());

    private static ConnectionProfile Profile(DatabaseDialect dialect) =>
        new(Guid.NewGuid(), "prod", dialect, "h", 5432, "d", "u", null, null);

    [Fact]
    public void Start_RunsLaunchAndStopCommandEndsIt()
    {
        var host = new FakeTerminalHost();
        using var tab = new TerminalTabViewModel(host, Launch);
        Assert.False(tab.StopCommand.CanExecute(null));

        tab.StartCommand.Execute(null);
        Assert.True(tab.IsRunning);
        Assert.Same(Launch, Assert.Single(host.Started));

        tab.StopCommand.Execute(null);
        Assert.False(tab.IsRunning);
        Assert.Equal(1, host.StopCount);
    }

    [Fact]
    public void Start_WhileRunning_StopsThenRestarts()
    {
        var host = new FakeTerminalHost();
        using var tab = new TerminalTabViewModel(host, Launch);
        tab.StartCommand.Execute(null);

        tab.StartCommand.Execute(null);

        Assert.Equal(2, host.Started.Count);
        Assert.Equal(1, host.StopCount);
        Assert.True(tab.IsRunning);
    }

    [Fact]
    public void Exited_ClearsIsRunning()
    {
        var host = new FakeTerminalHost();
        using var tab = new TerminalTabViewModel(host, Launch);
        tab.StartCommand.Execute(null);

        host.RaiseExited();

        Assert.False(tab.IsRunning);
        Assert.False(tab.StopCommand.CanExecute(null));
    }

    [Fact]
    public async Task NewTerminal_WithoutProfile_StartsLocalShell()
    {
        var host = new PanelsHost();

        await host.Workspace.NewTerminalCommand.ExecuteAsync(null);

        var tab = Assert.Single(host.Workspace.Terminals);
        Assert.True(tab.IsRunning);
        Assert.Single(Assert.Single(host.TerminalHosts).Started);
    }

    [Fact]
    public async Task NewTerminal_WithProfile_StartsCliWithPasswordInEnvironment()
    {
        var host = new PanelsHost();

        await host.Workspace.NewTerminalCommand.ExecuteAsync(Profile(DatabaseDialect.PostgreSql));

        var launch = Assert.Single(Assert.Single(host.TerminalHosts).Started);
        Assert.Equal("/usr/bin/psql", launch.FileName);
        Assert.Equal("secret", launch.Environment["PGPASSWORD"]);
        Assert.StartsWith("prod", host.Workspace.Terminals[0].Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NewTerminal_MissingExecutable_ShowsErrorAndOpensNothing()
    {
        var host = new PanelsHost();

        await host.Workspace.NewTerminalCommand.ExecuteAsync(Profile(DatabaseDialect.MySql));

        Assert.Empty(host.Workspace.Terminals);
        Assert.Contains("mysql", Assert.Single(host.Dialogs.Errors).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CloseTerminal_RemovesAndStopsTab()
    {
        var host = new PanelsHost();
        await host.Workspace.NewTerminalCommand.ExecuteAsync(null);
        var tab = host.Workspace.Terminals[0];

        host.Workspace.CloseTerminalCommand.Execute(tab);

        Assert.Empty(host.Workspace.Terminals);
        Assert.Equal(1, host.TerminalHosts[0].StopCount);
    }
}
