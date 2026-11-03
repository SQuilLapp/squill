using System.Globalization;
using Squill.Platform.Settings;

namespace Squill.Presentation.Localization;

/// <summary>Turns the saved language choice into the language the application displays.</summary>
public static class LanguageResolver
{
    /// <summary>Resolves the display language.</summary>
    /// <param name="setting">The saved choice.</param>
    /// <param name="osUiCulture">The UI culture of the operating system, used for <see cref="AppLanguage.System"/>.</param>
    /// <returns>French when requested or when the system choice meets a French culture, English otherwise.</returns>
    public static UiLanguage Resolve(AppLanguage setting, CultureInfo osUiCulture)
    {
        ArgumentNullException.ThrowIfNull(osUiCulture);
        return setting switch
        {
            AppLanguage.French => UiLanguage.French,
            AppLanguage.English => UiLanguage.English,
            _ => osUiCulture.TwoLetterISOLanguageName == "fr" ? UiLanguage.French : UiLanguage.English,
        };
    }
}
