using Squill.Platform.Updates;

namespace Squill.Presentation.Updates;

/// <summary>Which controls of the update notice are shown for a status; no UI thread needed.</summary>
[Flags]
public enum BannerParts
{
    /// <summary>Nothing beyond the text.</summary>
    None = 0,

    /// <summary>The button that opens the update dialog.</summary>
    Details = 1,

    /// <summary>The button that starts the download.</summary>
    Download = 2,

    /// <summary>The download progress bar.</summary>
    Progress = 4,

    /// <summary>The button that restarts to apply the update.</summary>
    Restart = 8,

    /// <summary>The button that retries after a failure.</summary>
    Retry = 16,
}

/// <summary>Maps the update status to the parts of the notice.</summary>
public static class UpdateBannerLayout
{
    /// <summary>Gets the controls shown with the notice.</summary>
    /// <param name="status">The current update status.</param>
    /// <returns>The parts to show; <see cref="BannerParts.None"/> for statuses without a notice.</returns>
    public static BannerParts PartsFor(UpdateStatus status) => status switch
    {
        UpdateStatus.Available => BannerParts.Details | BannerParts.Download,
        UpdateStatus.Downloading => BannerParts.Details | BannerParts.Progress,
        UpdateStatus.Ready => BannerParts.Details | BannerParts.Restart,
        UpdateStatus.Failed => BannerParts.Details | BannerParts.Retry,
        _ => BannerParts.None,
    };

    /// <summary>Gets whether the notice warns instead of informing.</summary>
    /// <param name="status">The current update status.</param>
    /// <returns><see langword="true"/> when the last operation failed.</returns>
    public static bool IsWarning(UpdateStatus status) => status == UpdateStatus.Failed;
}
