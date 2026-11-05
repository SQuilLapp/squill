using System.Globalization;
using Squill.Core;
using Squill.Presentation.Localization;

namespace Squill.Presentation.Terminal;

/// <summary>Builds the command lines of the shell and CLI tabs.</summary>
public static class TerminalLauncher
{
    private static readonly IReadOnlyDictionary<string, string> NoEnvironment = new Dictionary<string, string>();

    /// <summary>Builds the command line of the database CLI matching a profile.</summary>
    /// <param name="profile">Connection profile.</param>
    /// <param name="password">Database password, passed through the environment only; may be <see langword="null"/>.</param>
    /// <param name="findOnPath">Resolves an executable name to its full path, or <see langword="null"/> when missing.</param>
    /// <param name="reason">The user-facing explanation when the result is <see langword="null"/>.</param>
    /// <param name="tunnelPort">Local port of the open SSH tunnel; the CLI then connects to <c>127.0.0.1</c> on that port.</param>
    /// <returns>The launch, or <see langword="null"/> when the CLI is missing or the profile lacks what it needs.</returns>
    public static TerminalLaunch? ForProfile(ConnectionProfile profile, string? password, Func<string, string?> findOnPath, out string? reason, int? tunnelPort = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(findOnPath);
        if (tunnelPort is { } local)
            profile = profile with { Host = "127.0.0.1", Port = local };
        var (exe, args, passwordVariable) = profile.Dialect switch
        {
            DatabaseDialect.PostgreSql => ("psql", Args("-h", profile.Host, "-p", Port(profile), "-U", profile.UserName, "-d", profile.Database), "PGPASSWORD"),
            DatabaseDialect.MySql => ("mysql", Args("-h", profile.Host, "-P", Port(profile), "-u", profile.UserName, "-D", profile.Database, MySqlSsl(profile)), "MYSQL_PWD"),
            DatabaseDialect.SqlServer => ("sqlcmd", SqlCmdArgs(profile), "SQLCMDPASSWORD"),
            DatabaseDialect.Sqlite => ("sqlite3", Args(profile.FilePath), null),
            _ => throw new ArgumentOutOfRangeException(nameof(profile)),
        };
        if (profile.Dialect == DatabaseDialect.Sqlite && args.Count == 0)
        {
            reason = UiText.Format("terminal.noDatabaseFile", profile.Name);
            return null;
        }
        if (findOnPath(exe) is not { } path)
        {
            reason = UiText.Format("terminal.cliNotFound", exe, profile.Dialect, InstallHint(exe));
            return null;
        }
        reason = null;
        var environment = passwordVariable is not null && !string.IsNullOrEmpty(password)
            ? new Dictionary<string, string> { [passwordVariable] = password }
            : NoEnvironment;
        return new TerminalLaunch(path, args, null, environment);
    }

    /// <summary>Builds the command line of the user's local shell.</summary>
    /// <returns>The launch of <c>pwsh</c>/<c>powershell</c> on Windows, the <c>SHELL</c> variable or <c>/bin/sh</c> elsewhere.</returns>
    public static TerminalLaunch LocalShell() =>
        LocalShell(FindOnPath, OperatingSystem.IsWindows(), Environment.GetEnvironmentVariable("SHELL"));

    /// <summary>Builds the command line of the local shell from explicit inputs.</summary>
    /// <param name="findOnPath">Resolves an executable name to its full path, or <see langword="null"/> when missing.</param>
    /// <param name="isWindows">Whether to pick a PowerShell.</param>
    /// <param name="shellVariable">Value of the <c>SHELL</c> environment variable.</param>
    /// <returns>The launch of the shell.</returns>
    public static TerminalLaunch LocalShell(Func<string, string?> findOnPath, bool isWindows, string? shellVariable)
    {
        ArgumentNullException.ThrowIfNull(findOnPath);
        var shell = isWindows
            ? findOnPath("pwsh") ?? findOnPath("powershell") ?? "powershell"
            : string.IsNullOrEmpty(shellVariable) ? "/bin/sh" : shellVariable;
        return new TerminalLaunch(shell, [], null, NoEnvironment);
    }

    /// <summary>Looks an executable up on the <c>PATH</c> (with <c>PATHEXT</c> on Windows), then in the default Windows install folders of the database clients.</summary>
    /// <param name="name">Executable name without extension.</param>
    /// <returns>The full path, or <see langword="null"/> when not found.</returns>
    public static string? FindOnPath(string name) =>
        FindOnPath(name, Environment.GetEnvironmentVariable("PATH"), OperatingSystem.IsWindows(), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));

    /// <summary>Looks an executable up in explicit directories.</summary>
    /// <param name="name">Executable name without extension.</param>
    /// <param name="path">Value of the <c>PATH</c> variable.</param>
    /// <param name="windows">Whether to use <c>PATHEXT</c> and search the default install folders.</param>
    /// <param name="programFiles">The <c>Program Files</c> folder holding the default installs.</param>
    /// <returns>The full path, or <see langword="null"/> when not found.</returns>
    public static string? FindOnPath(string name, string? path, bool windows, string programFiles)
    {
        ArgumentNullException.ThrowIfNull(name);
        var extensions = windows
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE").Split(';', StringSplitOptions.RemoveEmptyEntries)
            : [""];
        IEnumerable<string> directories = (path ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        if (windows)
            directories = directories.Concat(DefaultInstallFolders(programFiles));
        return (from directory in directories
                from extension in extensions
                select Path.Combine(directory, name + extension))
            .FirstOrDefault(File.Exists);
    }

    // shortcut: only the Program Files of the current process bitness and the three documented folders; upgrade when a client ships elsewhere.
    private static IEnumerable<string> DefaultInstallFolders(string programFiles)
    {
        foreach (var pattern in new[] { "PostgreSQL", "MySQL", Path.Combine("Microsoft SQL Server", "Client SDK", "ODBC") })
        {
            var root = Path.Combine(programFiles, pattern);
            if (!Directory.Exists(root))
                continue;
            foreach (var version in Directory.EnumerateDirectories(root).OrderDescending(StringComparer.Ordinal))
            {
                yield return Path.Combine(version, "bin");
                yield return Path.Combine(version, "Tools", "Binn");
            }
        }
    }

    private static string InstallHint(string exe) => exe switch
    {
        "psql" => UiText.Get("terminal.install.psql"),
        "mysql" => UiText.Get("terminal.install.mysql"),
        "sqlcmd" => UiText.Get("terminal.install.sqlcmd"),
        _ => UiText.Get("terminal.install.sqlite3"),
    };

    private static string Port(ConnectionProfile profile) =>
        profile.Port?.ToString(CultureInfo.InvariantCulture) ?? "";

    private static List<string> SqlCmdArgs(ConnectionProfile profile)
    {
        var server = profile.Port is { } port ? $"{profile.Host},{port}" : profile.Host;
        var args = Args("-S", server, "-U", profile.UserName, "-d", profile.Database);
        if (profile.TrustServerCertificate)
            args.Add("-C");
        return args;
    }

    // MariaDB clients use other option names, so the option is only sent to MySQL.
    private static string? MySqlSsl(ConnectionProfile profile) => profile.MariaDb ? null : profile.Tls switch
    {
        TlsMode.Disable => "--ssl-mode=DISABLED",
        TlsMode.Require => "--ssl-mode=REQUIRED",
        TlsMode.VerifyFull => "--ssl-mode=VERIFY_IDENTITY",
        _ => null,
    };

    // Drops empty values together with the flag that precedes them, so unset profile fields never yield a dangling option.
    private static List<string> Args(params string?[] parts)
    {
        var result = new List<string>();
        foreach (var part in parts)
        {
            if (!string.IsNullOrEmpty(part))
                result.Add(part);
            else if (result.Count > 0 && result[^1].StartsWith('-'))
                result.RemoveAt(result.Count - 1);
        }
        return result;
    }
}
