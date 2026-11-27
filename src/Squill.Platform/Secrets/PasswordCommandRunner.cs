using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Squill.Platform.Secrets;

/// <summary>Why <see cref="PasswordCommandRunner"/> could not produce a password.</summary>
public enum PasswordCommandFailure
{
    /// <summary>The command line is empty or has an unterminated quote.</summary>
    Invalid,
    /// <summary>The program could not be started.</summary>
    StartFailed,
    /// <summary>The program ran longer than the timeout and was killed.</summary>
    TimedOut,
    /// <summary>The program exited with a non-zero code.</summary>
    ExitCode,
    /// <summary>The program printed nothing but white space.</summary>
    EmptyOutput,
}

/// <summary>A password command failed; the exception never contains the standard output of the command.</summary>
public sealed class PasswordCommandException : Exception
{
    /// <summary>Initializes the exception.</summary>
    /// <param name="failure">Why the command failed.</param>
    /// <param name="detail">Exit code, or the start error; <see langword="null"/> when there is none.</param>
    /// <param name="standardError">Beginning of what the command wrote on standard error; <see langword="null"/> when nothing.</param>
    public PasswordCommandException(PasswordCommandFailure failure, string? detail = null, string? standardError = null)
        : base($"Password command failed: {failure}")
    {
        Failure = failure;
        Detail = detail;
        StandardError = standardError;
    }

    /// <summary>Gets why the command failed.</summary>
    public PasswordCommandFailure Failure { get; }

    /// <summary>Gets the exit code or the start error, if any.</summary>
    public string? Detail { get; }

    /// <summary>Gets the beginning of the standard error output, if any.</summary>
    public string? StandardError { get; }
}

/// <summary>Reads a password from the standard output of an external program run without a shell.</summary>
public interface IPasswordCommandRunner
{
    /// <summary>Runs the command and returns its trimmed standard output.</summary>
    /// <param name="commandLine">Program and arguments, split by <see cref="PasswordCommandRunner.Split"/>.</param>
    /// <param name="ct">Cancellation token; cancelling kills the program.</param>
    /// <returns>The password.</returns>
    /// <exception cref="PasswordCommandException">The command is invalid, failed, timed out or printed nothing.</exception>
    ValueTask<string> RunAsync(string commandLine, CancellationToken ct);
}

/// <summary>Runs the program with an argument list (no shell, no string concatenation), a closed standard input, a timeout and a bounded output; the output is never logged.</summary>
public sealed class PasswordCommandRunner : IPasswordCommandRunner
{
    private const int MaxChars = 65536;

    /// <summary>Initializes the runner.</summary>
    /// <param name="timeout">Time a command may run before it is killed; <see langword="null"/> for 10 seconds.</param>
    public PasswordCommandRunner(TimeSpan? timeout = null) => Timeout = timeout ?? TimeSpan.FromSeconds(10);

    /// <summary>Gets the time a command may run before it is killed.</summary>
    public TimeSpan Timeout { get; }

    /// <summary>Splits a command line into program and arguments: white space separates, double quotes group, a backslash is literal (Windows paths).</summary>
    /// <param name="commandLine">The command line.</param>
    /// <returns>The program followed by its arguments.</returns>
    /// <exception cref="PasswordCommandException">The line is empty or a quote is not closed.</exception>
    public static IReadOnlyList<string> Split(string commandLine)
    {
        List<string> parts = [];
        StringBuilder current = new();
        var inQuotes = false;
        var started = false;
        foreach (var c in commandLine ?? "")
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                started = true;
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (started)
                    parts.Add(current.ToString());
                current.Clear();
                started = false;
            }
            else
            {
                current.Append(c);
                started = true;
            }
        }

        if (started)
            parts.Add(current.ToString());
        return inQuotes || parts.Count == 0 || parts[0].Length == 0 ? throw new PasswordCommandException(PasswordCommandFailure.Invalid) : parts;
    }

    /// <inheritdoc/>
    public async ValueTask<string> RunAsync(string commandLine, CancellationToken ct)
    {
        var parts = Split(commandLine);
        ProcessStartInfo info = new(parts[0])
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            // Windows looks in the working directory before PATH: a neutral one stops a planted program.
            WorkingDirectory = OperatingSystem.IsWindows() ? Environment.SystemDirectory : "/",
        };
        foreach (var argument in parts.Skip(1))
            info.ArgumentList.Add(argument);

        using Process process = new() { StartInfo = info };
        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            throw new PasswordCommandException(PasswordCommandFailure.StartFailed, ex.Message);
        }

        process.StandardInput.Close();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);
        var output = ReadBoundedAsync(process.StandardOutput, timeout.Token);
        var error = ReadBoundedAsync(process.StandardError, timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            await Task.WhenAll(output, error);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            ct.ThrowIfCancellationRequested();
            throw new PasswordCommandException(PasswordCommandFailure.TimedOut);
        }

        var errorText = (await error).Trim();
        var shownError = errorText.Length == 0 ? null : errorText[..Math.Min(errorText.Length, 500)];
        if (process.ExitCode != 0)
            throw new PasswordCommandException(PasswordCommandFailure.ExitCode, process.ExitCode.ToString(CultureInfo.InvariantCulture), shownError);
        var password = (await output).Trim();
        return password.Length == 0 ? throw new PasswordCommandException(PasswordCommandFailure.EmptyOutput, null, shownError) : password;
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken ct)
    {
        StringBuilder text = new();
        var buffer = new char[1024];
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), ct)) > 0)
        {
            if (text.Length < MaxChars)
                text.Append(buffer, 0, read); // beyond the cap the rest is drained and dropped
        }

        return text.ToString();
    }
}
