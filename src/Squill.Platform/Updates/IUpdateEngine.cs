using Squill.Core;

namespace Squill.Platform.Updates;

/// <summary>The part of updating that depends on a packaging technology; <see cref="UpdateService"/> only talks to this.</summary>
public interface IUpdateEngine
{
    /// <summary>Gets a value indicating whether the application was installed by the installer (false for a dev run or tests).</summary>
    bool IsInstalled { get; }

    /// <summary>Gets the installed version, or <see langword="null"/> when not installed.</summary>
    SemanticVersion? InstalledVersion { get; }

    /// <summary>Looks for a newer version.</summary>
    /// <param name="channel">The channel to follow.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The offer, or <see langword="null"/> when up to date.</returns>
    /// <exception cref="UpdateEngineException">The check failed.</exception>
    ValueTask<UpdateOffer?> CheckAsync(UpdateChannel channel, CancellationToken ct);

    /// <summary>Downloads an offer.</summary>
    /// <param name="offer">An offer this engine returned.</param>
    /// <param name="percent">Receives progress from 0 to 100.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the update is ready to apply.</returns>
    /// <exception cref="UpdateEngineException">The download failed.</exception>
    ValueTask DownloadAsync(UpdateOffer offer, IProgress<int> percent, CancellationToken ct);

    /// <summary>Applies a downloaded offer and restarts the application; does not return when it succeeds.</summary>
    /// <param name="offer">A downloaded offer.</param>
    void ApplyAndRestart(UpdateOffer offer);
}
