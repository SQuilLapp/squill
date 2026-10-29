namespace Squill.Platform;

/// <summary>Locates the per-user directory where SQuilL keeps its files.</summary>
public sealed class AppPaths
{
    /// <summary>Initializes the paths.</summary>
    /// <param name="overrideDirectory">Directory to use instead of the platform default (tests).</param>
    public AppPaths(string? overrideDirectory = null)
    {
        DataDirectory = overrideDirectory ?? DefaultDirectory();
    }

    /// <summary>Gets the root directory of the application data.</summary>
    public string DataDirectory { get; }

    /// <summary>Combines <see cref="DataDirectory"/> with path segments and creates the containing directory.</summary>
    /// <param name="segments">Relative path segments, the last one usually being a file name.</param>
    /// <returns>The full path.</returns>
    public string Combine(params string[] segments)
    {
        var path = Path.Combine([DataDirectory, .. segments]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }

    private static string DefaultDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SQuilL");
        }

        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(home, "Library", "Application Support", "SQuilL");
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        return Path.Combine(string.IsNullOrEmpty(xdg) ? Path.Combine(home, ".config") : xdg, "squill");
    }
}
