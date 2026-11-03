using System.Text.RegularExpressions;
using Squill.Presentation.Localization;
using Xunit;

namespace Squill.Presentation.Tests.Localization;

public sealed partial class CatalogTests
{
    [GeneratedRegex(@"\{(\d+)[^}]*\}")]
    private static partial Regex Placeholder();

    // Keys whose English text is legitimately identical to the French one.
    private static readonly HashSet<string> SameInBothLanguages =
    [
        "settings.language.french", // language names are shown in their own language
        "settings.language.english", // language names are shown in their own language
        "dialog.ok", // same word
        "workspace.terminal", // same word
        "explorer.tables", // same word
        "properties.kindTable", // same word
        "results.type.date", // same word
        "results.exportJsonDetail", // file extension
        "status.production", // environment badge
        "status.staging", // environment badge
        "status.dev", // environment badge
        "status.local", // environment badge
        "status.position", // Ln / Col abbreviations
        "status.encoding", // UTF-8
        "editor.script", // same word
        "editor.snippets", // same word
        "connections.envProduction", // same word
        "connections.envStaging", // proper name
        "connections.envLocal", // same word
        "connections.tabSsh", // acronym
        "connections.port", // same word
        "designer.colPk", // acronym
        "designer.colType", // same word
        "designer.colNull", // SQL keyword
        "designer.colFk", // acronym
        "designer.unique", // same word
        "designer.onDelete", // SQL clause
        "designer.onUpdate", // SQL clause
        "er.types", // same word
        "er.tables", // TABLES · {0} / {1}, same word
        "er.title", // ER - {0}, same text
        "er.relationFrom", // only symbols and placeholders
    ];

    private static Dictionary<string, string> Fr => UiTextCatalogFr.Entries.ToDictionary(e => e.Key, e => e.Value);

    private static Dictionary<string, string> En => UiTextCatalogEn.Entries.ToDictionary(e => e.Key, e => e.Value);

    [Fact]
    public void Catalogs_HaveTheSameKeysInTheSameOrder() =>
        Assert.Equal(UiTextCatalogFr.Entries.Select(e => e.Key), UiTextCatalogEn.Entries.Select(e => e.Key));

    [Fact]
    public void Keys_AreUniqueInEachCatalog()
    {
        Assert.Empty(UiTextCatalogFr.Entries.GroupBy(e => e.Key).Where(g => g.Count() > 1).Select(g => g.Key));
        Assert.Empty(UiTextCatalogEn.Entries.GroupBy(e => e.Key).Where(g => g.Count() > 1).Select(g => g.Key));
    }

    [Fact]
    public void Values_AreNeverBlank() =>
        Assert.Empty(UiTextCatalogFr.Entries.Concat(UiTextCatalogEn.Entries).Where(e => string.IsNullOrWhiteSpace(e.Value)).Select(e => e.Key));

    [Fact]
    public void Placeholders_MatchPerKey()
    {
        var en = En;
        var mismatched = Fr.Where(p => Indexes(p.Value) != Indexes(en[p.Key])).Select(p => p.Key);
        Assert.Empty(mismatched);

        static string Indexes(string value) =>
            string.Join(",", Placeholder().Matches(value).Select(m => m.Groups[1].Value).Distinct().Order());
    }

    [Fact]
    public void PluralKeys_ComeInPairsInBothCatalogs()
    {
        foreach (var entries in new[] { UiTextCatalogFr.Entries, UiTextCatalogEn.Entries })
        {
            var keys = entries.Select(e => e.Key).ToHashSet();
            foreach (var one in keys.Where(k => k.EndsWith(".one", StringComparison.Ordinal)))
                Assert.Contains(one[..^4] + ".other", keys);
            foreach (var other in keys.Where(k => k.EndsWith(".other", StringComparison.Ordinal)))
                Assert.Contains(other[..^6] + ".one", keys);
        }
    }

    [Fact]
    public void EnglishDiffersFromFrench_ExceptOnTheAllowList()
    {
        var en = En;
        var same = Fr.Where(p => p.Value == en[p.Key] && !SameInBothLanguages.Contains(p.Key)).Select(p => p.Key);
        Assert.Empty(same);
    }
}
