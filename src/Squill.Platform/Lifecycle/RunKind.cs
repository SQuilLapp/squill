namespace Squill.Platform.Lifecycle;

/// <summary>How the current start relates to the previous one, derived from the last version that ran.</summary>
public enum RunKind
{
    /// <summary>The same version ran last time.</summary>
    Same,

    /// <summary>No usable state exists: first start after an install.</summary>
    FirstRun,

    /// <summary>A lower version ran last time: the app was updated.</summary>
    Updated,

    /// <summary>A higher version ran last time: the app was downgraded.</summary>
    Downgraded,
}
