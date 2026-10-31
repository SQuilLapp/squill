using Squill.Core;
using Squill.Presentation.Terminal;
using Xunit;

namespace Squill.Presentation.Tests.Terminal;

public sealed class TerminalLauncherTests
{
    private const string Secret = "s3cret";

    private static string? Everywhere(string name) => "/bin/" + name;

    private static ConnectionProfile Profile(DatabaseDialect dialect, string? filePath = null) =>
        new(Guid.NewGuid(), "p", dialect, "db.local", 1234, "shop", "alice", filePath, null);

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql, "/bin/psql", "-h db.local -p 1234 -U alice shop", "PGPASSWORD")]
    [InlineData(DatabaseDialect.MySql, "/bin/mysql", "-h db.local -P 1234 -u alice shop", "MYSQL_PWD")]
    [InlineData(DatabaseDialect.SqlServer, "/bin/sqlcmd", "-S db.local,1234 -U alice -d shop", "SQLCMDPASSWORD")]
    public void ForProfile_ServerDialect_BuildsArgumentsAndPasswordEnvironment(DatabaseDialect dialect, string exe, string args, string variable)
    {
        var launch = TerminalLauncher.ForProfile(Profile(dialect), Secret, Everywhere, out var reason);

        Assert.NotNull(launch);
        Assert.Null(reason);
        Assert.Equal(exe, launch.FileName);
        Assert.Equal(args, string.Join(' ', launch.Arguments));
        Assert.Equal(Secret, launch.Environment[variable]);
        Assert.DoesNotContain(launch.Arguments, a => a.Contains(Secret, StringComparison.Ordinal));
    }

    [Fact]
    public void ForProfile_Sqlite_OpensFileWithoutEnvironment()
    {
        var launch = TerminalLauncher.ForProfile(Profile(DatabaseDialect.Sqlite, "/data/a.db"), Secret, Everywhere, out _);

        Assert.NotNull(launch);
        Assert.Equal(["/data/a.db"], launch.Arguments);
        Assert.Empty(launch.Environment);
    }

    [Fact]
    public void ForProfile_UnsetFields_OmitTheirFlags()
    {
        var profile = Profile(DatabaseDialect.PostgreSql) with { Port = null, UserName = null, Database = null };

        var launch = TerminalLauncher.ForProfile(profile, null, Everywhere, out _);

        Assert.NotNull(launch);
        Assert.Equal(["-h", "db.local"], launch.Arguments);
        Assert.Empty(launch.Environment);
    }

    [Fact]
    public void ForProfile_SqliteWithoutFile_ReturnsNullAndReason()
    {
        var launch = TerminalLauncher.ForProfile(Profile(DatabaseDialect.Sqlite), null, Everywhere, out var reason);

        Assert.Null(launch);
        Assert.Contains("no database file", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ForProfile_MissingExecutable_ReturnsNullAndReason()
    {
        var launch = TerminalLauncher.ForProfile(Profile(DatabaseDialect.MySql), Secret, _ => null, out var reason);

        Assert.Null(launch);
        Assert.Contains("mysql", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalShell_Windows_PrefersPwshThenPowershell()
    {
        Assert.Equal("/bin/pwsh", TerminalLauncher.LocalShell(Everywhere, true, null).FileName);
        Assert.Equal("/bin/powershell", TerminalLauncher.LocalShell(n => n == "powershell" ? "/bin/powershell" : null, true, null).FileName);
    }

    [Theory]
    [InlineData("/usr/bin/zsh", "/usr/bin/zsh")]
    [InlineData(null, "/bin/sh")]
    [InlineData("", "/bin/sh")]
    public void LocalShell_Unix_UsesShellVariableOrBinSh(string? variable, string expected) =>
        Assert.Equal(expected, TerminalLauncher.LocalShell(Everywhere, false, variable).FileName);
}
