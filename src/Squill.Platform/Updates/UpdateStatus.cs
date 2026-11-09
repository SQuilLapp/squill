namespace Squill.Platform.Updates;

/// <summary>Where the <see cref="UpdateService"/> is in the update flow.</summary>
public enum UpdateStatus
{
    /// <summary>Nothing is running and nothing is known.</summary>
    Idle,

    /// <summary>A check is running.</summary>
    Checking,

    /// <summary>The last check found nothing newer.</summary>
    UpToDate,

    /// <summary>A newer version can be downloaded.</summary>
    Available,

    /// <summary>The update is downloading.</summary>
    Downloading,

    /// <summary>The update is downloaded and waits for a restart.</summary>
    Ready,

    /// <summary>The last operation failed; see <see cref="UpdateService.Failure"/>.</summary>
    Failed,

    /// <summary>The application is not installed, so updates are unavailable.</summary>
    NotInstalled,
}
