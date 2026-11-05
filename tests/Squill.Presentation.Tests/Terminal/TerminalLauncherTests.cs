using Squill.Core;
using Squill.Presentation.Localization;
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
    [InlineData(DatabaseDialect.PostgreSql, "/bin/psql", "-h db.local -p 1234 -U alice -d shop", "PGPASSWORD")]
    [InlineData(DatabaseDialect.MySql, "/bin/mysql", "-h db.local -P 1234 -u alice -D shop", "MYSQL_PWD")]
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
        using var language = UiText.Override(UiLanguage.English);

        var launch = TerminalLauncher.ForProfile(Profile(DatabaseDialect.Sqlite), null, Everywhere, out var reason);

        Assert.Null(launch);
        Assert.Equal("Connection 'p' has no database file.", reason);
    }

    [Fact]
    public void ForProfile_SqliteWithoutFile_ReasonIsFrench()
    {
        using var language = UiText.Override(UiLanguage.French);

        TerminalLauncher.ForProfile(Profile(DatabaseDialect.Sqlite), null, Everywhere, out var reason);

        Assert.Equal("La connexion 'p' n'a pas de fichier de base de données.", reason);
    }

    [Fact]
    public void ForProfile_MissingExecutable_ReturnsNullAndReason()
    {
        var launch = TerminalLauncher.ForProfile(Profile(DatabaseDialect.MySql), Secret, _ => null, out var reason);

        Assert.Null(launch);
        Assert.Contains("mysql", reason, StringComparison.Ordinal);
        Assert.Contains("PATH", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ForProfile_Tunnel_ConnectsToLoopbackOnLocalPort()
    {
        var launch = TerminalLauncher.ForProfile(Profile(DatabaseDialect.PostgreSql), Secret, Everywhere, out _, tunnelPort: 50001);

        Assert.NotNull(launch);
        Assert.Equal("-h 127.0.0.1 -p 50001 -U alice -d shop", string.Join(' ', launch.Arguments));
    }

    [Fact]
    public void ForProfile_SqlServerTunnel_UsesLoopbackAndTrustFlag()
    {
        var profile = Profile(DatabaseDialect.SqlServer) with { TrustServerCertificate = true };

        var launch = TerminalLauncher.ForProfile(profile, Secret, Everywhere, out _, tunnelPort: 50002);

        Assert.NotNull(launch);
        Assert.Equal("-S 127.0.0.1,50002 -U alice -d shop -C", string.Join(' ', launch.Arguments));
    }

    [Theory]
    [InlineData(TlsMode.Default, null)]
    [InlineData(TlsMode.Disable, "--ssl-mode=DISABLED")]
    [InlineData(TlsMode.Require, "--ssl-mode=REQUIRED")]
    [InlineData(TlsMode.VerifyFull, "--ssl-mode=VERIFY_IDENTITY")]
    public void ForProfile_MySqlTls_MapsToSslMode(TlsMode tls, string? option)
    {
        var launch = TerminalLauncher.ForProfile(Profile(DatabaseDialect.MySql) with { Tls = tls }, null, Everywhere, out _);

        Assert.NotNull(launch);
        Assert.Equal(option is null ? 0 : 1, launch.Arguments.Count(a => a.StartsWith("--ssl-mode", StringComparison.Ordinal)));
        if (option is not null)
            Assert.Contains(option, launch.Arguments);
    }

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql)]
    [InlineData(DatabaseDialect.MySql)]
    [InlineData(DatabaseDialect.SqlServer)]
    public void ForProfile_NamesWithSpacesAndQuotes_StayOneQuotedArgument(DatabaseDialect dialect)
    {
        var profile = Profile(dialect) with { Database = "my \"shop\" db", UserName = "o'neil" };

        var launch = TerminalLauncher.ForProfile(profile, Secret, Everywhere, out _);

        Assert.NotNull(launch);
        Assert.Contains("my \"shop\" db", launch.Arguments);
        Assert.Contains("o'neil", launch.Arguments);
        Assert.Contains("\"my \\\"shop\\\" db\"", TerminalCommandLine.Build(launch, windows: true), StringComparison.Ordinal);
        Assert.Contains(@"'o'\''neil'",TerminalCommandLine.Build(launch, windows: false), StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, TerminalCommandLine.Build(launch, windows: true), StringComparison.Ordinal);
    }

    [Fact]
    public void ForProfile_SqlitePathWithSpaces_IsQuoted()
    {
        var launch = TerminalLauncher.ForProfile(Profile(DatabaseDialect.Sqlite, "/my data/a b.db"), null, Everywhere, out _);

        Assert.NotNull(launch);
        Assert.Equal("/bin/sqlite3 '/my data/a b.db'", TerminalCommandLine.Build(launch, windows: false));
        Assert.Equal("/bin/sqlite3 \"/my data/a b.db\"", TerminalCommandLine.Build(launch, windows: true));
    }

    [Theory]
    [InlineData(DatabaseDialect.PostgreSql, "psql was not found")]
    [InlineData(DatabaseDialect.MySql, "mysql was not found")]
    [InlineData(DatabaseDialect.SqlServer, "sqlcmd was not found")]
    public void ForProfile_MissingClient_MessageNamesClientWithInstallHint(DatabaseDialect dialect, string expected)
    {
        using var language = UiText.Override(UiLanguage.English);

        TerminalLauncher.ForProfile(Profile(dialect), null, _ => null, out var reason);

        Assert.Contains(expected, reason, StringComparison.Ordinal);
        Assert.Contains("Install", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ForProfile_MissingClient_MessageIsFrench()
    {
        using var language = UiText.Override(UiLanguage.French);

        TerminalLauncher.ForProfile(Profile(DatabaseDialect.PostgreSql), null, _ => null, out var reason);

        Assert.Contains("psql est introuvable", reason, StringComparison.Ordinal);
        Assert.Contains("Installez", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void FindOnPath_Windows_FallsBackToDefaultInstallFolders()
    {
        var root = Path.Combine(Path.GetTempPath(), "squill-" + Guid.NewGuid().ToString("N"));
        try
        {
            var bin = Path.Combine(root, "PostgreSQL", "17", "bin");
            Directory.CreateDirectory(bin);
            File.WriteAllText(Path.Combine(bin, "psql.exe"), "");
            var sqlcmd = Path.Combine(root, "Microsoft SQL Server", "Client SDK", "ODBC", "170", "Tools", "Binn");
            Directory.CreateDirectory(sqlcmd);
            File.WriteAllText(Path.Combine(sqlcmd, "sqlcmd.exe"), "");
            Environment.SetEnvironmentVariable("PATHEXT", ".EXE");

            Assert.Equal(Path.Combine(bin, "psql.exe"), TerminalLauncher.FindOnPath("psql", "", true, root), ignoreCase: true);
            Assert.Equal(Path.Combine(sqlcmd, "sqlcmd.exe"), TerminalLauncher.FindOnPath("sqlcmd", "", true, root), ignoreCase: true);
            Assert.Null(TerminalLauncher.FindOnPath("mysql", "", true, root));
            Assert.Null(TerminalLauncher.FindOnPath("psql", "", false, root));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ForProfile_InstalledSqlite3_RunsVersionThroughTheLauncherPath()
    {
        var launch = TerminalLauncher.ForProfile(Profile(DatabaseDialect.Sqlite, "a.db"), null, TerminalLauncher.FindOnPath, out var reason);
        if (launch is null)
        {
            Console.WriteLine("Smoke test skipped: " + reason);
            return;
        }
        var info = new System.Diagnostics.ProcessStartInfo(launch.FileName) { RedirectStandardOutput = true, UseShellExecute = false };
        info.ArgumentList.Add("--version");
        using var process = System.Diagnostics.Process.Start(info)!;
        var output = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, process.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(output));
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
