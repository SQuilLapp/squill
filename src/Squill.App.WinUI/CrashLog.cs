using System.Globalization;
using System.Text;

namespace Squill.App.WinUI;

/// <summary>Appends unhandled exceptions to a text file, because a XAML exception that leaves a callback only reaches Windows as a stowed exception with no managed details.</summary>
internal static class CrashLog
{
    private static readonly Lock Gate = new();
    private static string? _path;

    /// <summary>Starts listening to the unhandled-exception sources of the process other than the XAML application's own (which calls <see cref="Write"/>).</summary>
    /// <param name="path">The file the entries are appended to.</param>
    public static void Install(string path)
    {
        _path = path;
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Write("AppDomain.UnhandledException", e.ExceptionObject as Exception, e.ExceptionObject?.ToString());
        TaskScheduler.UnobservedTaskException += (_, e) => Write("TaskScheduler.UnobservedTaskException", e.Exception, null);
    }

    /// <summary>Builds one log entry.</summary>
    /// <param name="source">Which handler caught the exception.</param>
    /// <param name="exception">The exception, or <see langword="null"/> when only a message is known.</param>
    /// <param name="message">A fallback text used when there is no exception.</param>
    /// <param name="now">The entry time.</param>
    /// <returns>The entry: a header line, then type, message and stack of the exception and of each inner one.</returns>
    public static string Format(string source, Exception? exception, string? message, DateTimeOffset now)
    {
        var text = new StringBuilder().Append(now.ToString("O", CultureInfo.InvariantCulture)).Append(' ').AppendLine(source);
        if (exception is null)
            return text.AppendLine(message ?? "(no exception)").AppendLine().ToString();
        for (var e = exception; e is not null; e = e.InnerException)
            text.Append(e.GetType().FullName).Append(": ").AppendLine(e.Message).AppendLine(e.StackTrace);
        return text.AppendLine().ToString();
    }

    /// <summary>Appends an entry; never throws.</summary>
    /// <param name="source">Which handler caught the exception.</param>
    /// <param name="exception">The exception, or <see langword="null"/> when only a message is known.</param>
    /// <param name="message">A fallback text used when there is no exception.</param>
    public static void Write(string source, Exception? exception, string? message)
    {
        try
        {
            if (_path is null)
                return;
            lock (Gate)
                File.AppendAllText(_path, Format(source, exception, message, DateTimeOffset.Now));
        }
        catch (IOException)
        {
            // The log must never raise a second crash.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
