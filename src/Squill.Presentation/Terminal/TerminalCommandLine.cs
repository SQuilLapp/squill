using System.Text;

namespace Squill.Presentation.Terminal;

/// <summary>Turns a <see cref="TerminalLaunch"/> into the single command-line string that pseudo-terminal APIs take.</summary>
public static class TerminalCommandLine
{
    /// <summary>Builds the command line for the current operating system.</summary>
    /// <param name="launch">What to run.</param>
    /// <returns>The quoted command line.</returns>
    public static string Build(TerminalLaunch launch) => Build(launch, OperatingSystem.IsWindows());

    /// <summary>Builds the command line with the quoting rules of an operating system.</summary>
    /// <param name="launch">What to run.</param>
    /// <param name="windows"><see langword="true"/> for Windows double quotes, <see langword="false"/> for POSIX single quotes.</param>
    /// <returns>The file name and arguments, each quoted only when it needs it.</returns>
    public static string Build(TerminalLaunch launch, bool windows)
    {
        ArgumentNullException.ThrowIfNull(launch);
        var line = new StringBuilder(Quote(launch.FileName, windows));
        foreach (var argument in launch.Arguments)
            line.Append(' ').Append(Quote(argument, windows));
        return line.ToString();
    }

    private static string Quote(string value, bool windows)
    {
        if (value.Length > 0 && value.AsSpan().IndexOfAny(windows ? WindowsSpecials : PosixSpecials) < 0)
            return value;
        if (!windows)
            return "'" + value.Replace("'", @"'\''", StringComparison.Ordinal) + "'";
        // shortcut: backslashes directly before an embedded quote are not doubled, only a trailing run is; upgrade if an argument ever needs it.
        var body = value.TrimEnd('\\');
        return "\"" + body.Replace("\"", "\\\"", StringComparison.Ordinal) + new string('\\', (value.Length - body.Length) * 2) + "\"";
    }

    private const string WindowsSpecials = " \t\"";
    private const string PosixSpecials = " \t\"'\\";
}
