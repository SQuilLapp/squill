using System.Globalization;
using Squill.Platform.Settings;

namespace Squill.Presentation.Localization;

/// <summary>Resolves the language once at startup and applies it to <see cref="UiText"/> and the .NET cultures.</summary>
public static class LanguageSetup
{
    /// <summary>Applies the language; call it before any view-model or window is created.</summary>
    /// <param name="store">The settings; read synchronously, once, because this runs before any window exists and the file is tiny.</param>
    /// <param name="environmentOverride">Value of <c>SQUILL_LANGUAGE</c> (<c>fr</c>, <c>en</c> or <c>system</c>, case-insensitive) that replaces the saved choice without saving it; anything else is ignored.</param>
    /// <param name="osUiCulture">The UI culture of the operating system.</param>
    /// <returns>The language now in effect.</returns>
    public static UiLanguage Apply(ISettingsStore store, string? environmentOverride, CultureInfo osUiCulture)
    {
        ArgumentNullException.ThrowIfNull(store);
        // Task.Run keeps the read off the UI synchronization context: blocking on it from the UI thread would deadlock.
        var setting = Task.Run(() => store.LoadAsync(CancellationToken.None).AsTask()).GetAwaiter().GetResult().Language;
        if (environmentOverride?.Trim().ToLowerInvariant() switch
        {
            "fr" => AppLanguage.French,
            "en" => AppLanguage.English,
            "system" => AppLanguage.System,
            _ => (AppLanguage?)null,
        } is { } overridden)
            setting = overridden;

        var language = LanguageResolver.Resolve(setting, osUiCulture);
        UiText.Initialize(language);
        var culture = UiText.Culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        return language;
    }
}
