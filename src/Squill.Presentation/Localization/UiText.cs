using System.Collections.Frozen;
using System.Globalization;

namespace Squill.Presentation.Localization;

/// <summary>Looks up the user-visible strings of <see cref="UiTextCatalogFr"/> and <see cref="UiTextCatalogEn"/> in the current language; AOT-safe, no resource files.</summary>
public static class UiText
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");
    private static readonly FrozenDictionary<string, string> French = UiTextCatalogFr.Entries.ToFrozenDictionary(e => e.Key, e => e.Value);
    private static readonly FrozenDictionary<string, string> English = UiTextCatalogEn.Entries.ToFrozenDictionary(e => e.Key, e => e.Value);
    private static readonly AsyncLocal<UiLanguage?> Overridden = new();
    private static UiLanguage _initialized = UiLanguage.French;

    /// <summary>Gets the language in effect: the <see cref="Override"/> of the current async flow, else the one given to <see cref="Initialize"/> (French until then).</summary>
    public static UiLanguage Language => Overridden.Value ?? _initialized;

    /// <summary>Gets the culture used to format placeholders and the numbers and dates shown in <see cref="Language"/>: <c>fr-FR</c> or <c>en-US</c>.</summary>
    public static CultureInfo Culture => Language == UiLanguage.English ? EnglishCulture : FrenchCulture;

    /// <summary>Sets the language of the application; called once at startup.</summary>
    /// <param name="language">The language to use from now on.</param>
    public static void Initialize(UiLanguage language) => _initialized = language;

    /// <summary>Overrides the language for the current async flow. Exists for tests only (parallel-safe, flows through <see langword="await"/>); no production code may call it.</summary>
    /// <param name="language">The language to use until the returned scope is disposed.</param>
    /// <returns>A scope that restores the previous override when disposed.</returns>
    public static IDisposable Override(UiLanguage language)
    {
        var previous = Overridden.Value;
        Overridden.Value = language;
        return new Restore(previous);
    }

    /// <summary>Tells whether a key is in the catalog.</summary>
    /// <param name="key">Catalog key.</param>
    /// <returns><see langword="true"/> when the current language has a text for <paramref name="key"/>.</returns>
    public static bool Has(string key) => Table().ContainsKey(key);

    /// <summary>Gets the string of a catalog key.</summary>
    /// <param name="key">English dotted identifier such as <c>explorer.title</c>.</param>
    /// <returns>The text in the current language, or <c>!key!</c> when the key is not in the catalog.</returns>
    public static string Get(string key) => Table().TryGetValue(key, out var value) ? value : $"!{key}!";

    /// <summary>Gets a catalog string and fills its <c>{0}</c> placeholders using <see cref="Culture"/>.</summary>
    /// <param name="key">Catalog key.</param>
    /// <param name="args">Values for the placeholders.</param>
    /// <returns>The formatted text, or <c>!key!</c> when the key is not in the catalog.</returns>
    public static string Format(string key, params object?[] args) =>
        Table().TryGetValue(key, out var value) ? string.Format(Culture, value, args) : $"!{key}!";

    /// <summary>Formats a countable message, choosing between <c>baseKey.one</c> and <c>baseKey.other</c> by the plural rule of the current language.</summary>
    /// <param name="baseKey">Catalog key without the <c>.one</c> / <c>.other</c> suffix.</param>
    /// <param name="count">The count, passed as <c>{0}</c>; "one" is 0 or 1 in French and exactly 1 in English.</param>
    /// <param name="args">Further values, passed as <c>{1}</c>, <c>{2}</c>...</param>
    /// <returns>The formatted text, or <c>!key!</c> when the chosen key is not in the catalog.</returns>
    public static string FormatPlural(string baseKey, int count, params object?[] args)
    {
        var one = Language == UiLanguage.English ? count == 1 : Math.Abs((long)count) < 2;
        object?[] all = [count, .. args];
        return Format(one ? baseKey + ".one" : baseKey + ".other", all);
    }

    private static FrozenDictionary<string, string> Table() => Language == UiLanguage.English ? English : French;

    private sealed class Restore(UiLanguage? previous) : IDisposable
    {
        public void Dispose() => Overridden.Value = previous;
    }
}
