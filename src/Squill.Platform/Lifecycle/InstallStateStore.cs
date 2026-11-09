using System.Text.Json;

namespace Squill.Platform.Lifecycle;

/// <summary>Reads and writes <c>install-state.json</c> in the data directory (synchronous: a file of one line, read once at startup).</summary>
public sealed class InstallStateStore
{
    private readonly string _path;

    /// <summary>Initializes the store.</summary>
    /// <param name="paths">Application paths; the file lives in <see cref="AppPaths.DataDirectory"/>.</param>
    public InstallStateStore(AppPaths paths) => _path = paths.Combine(InstallLifecycle.StateFileName);

    /// <summary>Reads the stored state.</summary>
    /// <returns>The state, or <see langword="null"/> when the file is missing, unreadable or corrupt.</returns>
    public InstallState? Load()
    {
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(_path), InstallStateJsonContext.Default.InstallState);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Writes the state, replacing the file atomically.</summary>
    /// <param name="state">The state to store.</param>
    public void Save(InstallState state)
    {
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, InstallStateJsonContext.Default.InstallState));
        File.Move(temp, _path, overwrite: true);
    }

    /// <summary>Classifies the start against the stored version, then stores <paramref name="currentVersion"/>.</summary>
    /// <param name="currentVersion">SemVer 2 version of the running build.</param>
    /// <returns>The kind of start. A file that cannot be written is ignored: the next start then simply classifies again.</returns>
    public RunKind Record(string currentVersion)
    {
        var kind = InstallLifecycle.Classify(Load()?.LastRunVersion, currentVersion);
        try
        {
            Save(new InstallState(currentVersion));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Read-only data directory: the app still starts, only the "what is new" hint may repeat.
        }

        return kind;
    }
}
