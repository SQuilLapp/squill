namespace Squill.Platform.Settings;

/// <summary>Persists the user preferences.</summary>
public interface ISettingsStore
{
    /// <summary>Loads the settings.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The saved settings with values clamped, or <see cref="AppSettings.Default"/> when none are usable.</returns>
    ValueTask<AppSettings> LoadAsync(CancellationToken ct);

    /// <summary>Saves the settings, replacing the previous ones.</summary>
    /// <param name="settings">Settings to save.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask SaveAsync(AppSettings settings, CancellationToken ct);
}
