namespace Squill.Platform.Updates;

/// <summary>Creates the update engine used by the application.</summary>
public static class UpdateEngineFactory
{
    /// <summary>Creates the Velopack engine.</summary>
    /// <param name="repositoryUrl">The GitHub repository that hosts the releases.</param>
    /// <param name="feedOverride">A directory path or an http(s) URL that replaces the feed (<c>SQUILL_UPDATE_FEED</c>); null or empty means GitHub.</param>
    /// <returns>The engine.</returns>
    public static IUpdateEngine Create(string repositoryUrl, string? feedOverride) =>
        new VelopackUpdateEngine(repositoryUrl, feedOverride);
}
