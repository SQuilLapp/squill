using System.Globalization;
using Squill.Core;

namespace Squill.Platform.Lifecycle;

/// <summary>Decides whether a start is a first run, an update, a downgrade or a plain restart.</summary>
public static class InstallLifecycle
{
    /// <summary>File name of the persisted <see cref="InstallState"/> inside <see cref="AppPaths.DataDirectory"/>.</summary>
    public const string StateFileName = "install-state.json";

    /// <summary>Compares the version that ran last with the running one.</summary>
    /// <param name="lastRunVersion">Version stored by the previous run, or <see langword="null"/> when there is none.</param>
    /// <param name="currentVersion">SemVer 2 version of the running build.</param>
    /// <returns><see cref="RunKind.FirstRun"/> when the stored version is missing or unreadable, otherwise the order of the two versions.</returns>
    /// <exception cref="FormatException"><paramref name="currentVersion"/> is not a valid SemVer 2 version.</exception>
    public static RunKind Classify(string? lastRunVersion, string currentVersion)
    {
        var current = SemanticVersion.Parse(currentVersion, CultureInfo.InvariantCulture);
        if (!SemanticVersion.TryParse(lastRunVersion, out var last))
        {
            return RunKind.FirstRun;
        }

        return last.CompareTo(current) switch
        {
            < 0 => RunKind.Updated,
            > 0 => RunKind.Downgraded,
            _ => RunKind.Same,
        };
    }
}
