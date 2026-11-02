using System.Globalization;
using Squill.Core;

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
    /// <returns>The launch, or <see langword="null"/> when the CLI is missing or the profile lacks what it needs.</returns>
    public static TerminalLaunch? ForProfile(ConnectionProfile profile, string? password, Func<string, string?> findOnPath, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(findOnPath);
        var (exe, args, passwordVariable) = profile.Dialect switch
        {
            DatabaseDialect.PostgreSql => ("psql", Args("-h", profile.Host, "-p", Port(profile), "-U", profile.UserName, profile.Database), "PGPASSWORD"),
            DatabaseDialect.MySql => ("mysql", Args("-h", profile.Host, "-P", Port(profile), "-u", profile.UserName, profile.Database), "MYSQL_PWD"),
            DatabaseDialect.SqlServer => ("sqlcmd", SqlCmdArgs(profile), "SQLCMDPASSWORD"),
            DatabaseDialect.Sqlite => ("sqlite3", Args(profile.FilePath), null),
            _ => throw new ArgumentOutOfRangeException(nameof(profile)),
        };
        if (profile.Dialect == DatabaseDialect.Sqlite && args.Count == 0)
        {
            reason = $"Connection '{profile.Name}' has no database file.";
            return null;
        }
        if (findOnPath(exe) is not { } path)
        {
            reason = $"'{exe}' was not found on PATH; install it to open a {profile.Dialect} terminal.";
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

    /// <summary>Looks an executable up on the <c>PATH</c> (with <c>PATHEXT</c> on Windows).</summary>
    /// <param name="name">Executable name without extension.</param>
    /// <returns>The full path, or <see langword="null"/> when not found.</returns>
    public static string? FindOnPath(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE").Split(';', StringSplitOptions.RemoveEmptyEntries)
            : [""];
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        return (from directory in directories
                from extension in extensions
                select Path.Combine(directory, name + extension))
            .FirstOrDefault(File.Exists);
    }

    private static string Port(ConnectionProfile profile) =>
        profile.Port?.ToString(CultureInfo.InvariantCulture) ?? "";

    private static List<string> SqlCmdArgs(ConnectionProfile profile)
    {
        var server = profile.Port is { } port ? $"{profile.Host},{port}" : profile.Host;
        return Args("-S", server, "-U", profile.UserName, "-d", profile.Database);
    }

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
