namespace Squill.Presentation.Terminal;

/// <summary>Describes the process a terminal tab runs; secrets travel in <paramref name="Environment"/>, never in <paramref name="Arguments"/>.</summary>
/// <param name="FileName">Executable to start, resolved when possible.</param>
/// <param name="Arguments">Command-line arguments.</param>
/// <param name="WorkingDirectory">Working directory, or <see langword="null"/> for the host's default.</param>
/// <param name="Environment">Environment variables added to the process.</param>
public sealed record TerminalLaunch(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory,
    IReadOnlyDictionary<string, string> Environment);
