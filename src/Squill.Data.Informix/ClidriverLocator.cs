namespace Squill.Data.Informix;

/// <summary>Finds IBM's <c>db2cli64.dll</c> in an explicit folder only: never <c>PATH</c>, never the registry.</summary>
internal static class ClidriverLocator
{
    /// <summary>Environment variable holding the <c>clidriver</c> folder (the one that contains <c>bin</c>).</summary>
    public const string Variable = "SQUILL_CLIDRIVER";

    /// <summary>Gets the full path of <c>db2cli64.dll</c> for the running process.</summary>
    /// <returns>The path.</returns>
    /// <exception cref="DllNotFoundException">The folder or the file is missing.</exception>
    public static string LibraryPath() => Locate(Environment.GetEnvironmentVariable, AppContext.BaseDirectory, File.Exists);

    /// <summary>Resolves the library path from an explicit folder.</summary>
    /// <param name="getVariable">Reads an environment variable.</param>
    /// <param name="appFolder">The application folder, used with <c>clidriver</c> when the variable is unset.</param>
    /// <param name="fileExists">Tells whether a file exists.</param>
    /// <returns>The path of <c>bin/db2cli64.dll</c>.</returns>
    /// <exception cref="DllNotFoundException">The file is missing; the message names the variable and the folder searched.</exception>
    public static string Locate(Func<string, string?> getVariable, string appFolder, Func<string, bool> fileExists)
    {
        var configured = getVariable(Variable);
        var folder = string.IsNullOrWhiteSpace(configured) ? Path.Combine(appFolder, "clidriver") : configured;
        var path = Path.Combine(folder, "bin", OdbcNative.Library);
        return fileExists(path)
            ? path
            : throw new DllNotFoundException($"{OdbcNative.Library} was not found at '{path}'. Set {Variable} to the IBM clidriver folder (the one containing bin), or place it in '{Path.Combine(appFolder, "clidriver")}'.");
    }
}
