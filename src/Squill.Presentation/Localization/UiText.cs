using System.Collections.Frozen;
using System.Globalization;

namespace Squill.Presentation.Localization;

/// <summary>Looks up the French user-visible strings of <see cref="UiTextCatalog"/>; AOT-safe, no resource files.</summary>
public static class UiText
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly FrozenDictionary<string, string> Table = UiTextCatalog.Entries.ToFrozenDictionary(e => e.Key, e => e.Value);

    /// <summary>Gets the string of a catalog key.</summary>
    /// <param name="key">English dotted identifier such as <c>explorer.title</c>.</param>
    /// <returns>The French text, or <c>!key!</c> when the key is not in the catalog.</returns>
    public static string Get(string key) => Table.TryGetValue(key, out var value) ? value : $"!{key}!";

    /// <summary>Gets a catalog string and fills its <c>{0}</c> placeholders using the <c>fr-FR</c> culture.</summary>
    /// <param name="key">Catalog key.</param>
    /// <param name="args">Values for the placeholders.</param>
    /// <returns>The formatted text, or <c>!key!</c> when the key is not in the catalog.</returns>
    public static string Format(string key, params object?[] args) =>
        Table.TryGetValue(key, out var value) ? string.Format(Culture, value, args) : $"!{key}!";
}
