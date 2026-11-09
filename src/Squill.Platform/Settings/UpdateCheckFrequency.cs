namespace Squill.Platform.Settings;

/// <summary>How often the application may check for a new version on its own.</summary>
public enum UpdateCheckFrequency
{
    /// <summary>The user was never asked; no automatic network call is made.</summary>
    NotAsked,

    /// <summary>The user declined automatic checks.</summary>
    Never,

    /// <summary>Check at every start.</summary>
    Startup,

    /// <summary>Check at most once a day.</summary>
    Daily,

    /// <summary>Check at most once a week.</summary>
    Weekly,
}
