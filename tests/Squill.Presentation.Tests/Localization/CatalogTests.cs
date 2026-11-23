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
        "workspace.dropTablesLabel.one", // "{0} table" reads the same in both languages
        "workspace.dropTablesLabel.other", // "{0} tables" reads the same in both languages
        "explorer.filter.note", // "({0})" is only a placeholder in parentheses
        "home.newConnection.subtitle", // list of engine names
        "home.env.prod", // environment pill, same short form in both languages
        "import.step.source", // step name, same word in both languages
        "import.step.options", // step name
        "import.error.title", // dialog title
        "import.fileSize", // bare size, no wording
        "import.status.ok", // status word
        "import.ui.table", // same word in both languages
        "import.ui.colConversion", // same word in both languages
        "import.ui.quote.double", // symbol and word identical in both languages
        "home.env.staging", // environment pill
        "home.env.dev", // environment pill
        "home.env.local", // environment pill
        "compare.source", // SOURCE label of the compare header, same in both languages
        "activity.session", // SESSION caption of the detail pane, same word
        "activity.col.pid", // technical column name
        "security.connectedAs", // user@connection / database, language-neutral
        "activity.col.application", // column title, same word
        "activity.col.client", // column title, same word
        "compare.env.production", // environment name in the status bar
        "compare.env.staging", // environment name in the status bar
        "compare.env.development", // environment name in the status bar
        "compare.env.local", // environment name in the status bar
        "compare.group.table", // group title, same word
        "compare.duration", // seconds, same unit
        "compare.sourceCaption", // same word
        "activity.pause", // same word
        "activity.duration.ms", // unit, same in both languages
        "activity.duration.s", // unit
        "activity.duration.minS", // units
        "activity.duration.hMin", // units
        "activity.field.application", // same word
        "activity.field.client", // same word
        "activity.confirm.who", // pid and login, language-neutral
        "activity.tile.transactions", // same label
        "results.filter.op.greaterThan", // comparison symbol
        "results.filter.op.greaterOrEqual", // comparison symbol
        "results.filter.op.lessThan", // comparison symbol
        "results.filter.op.lessOrEqual", // comparison symbol
        "settings.language.french", // language names are shown in their own language
        "settings.language.english", // language names are shown in their own language
        "dialog.ok", // same word
        "workspace.terminal", // same word
        "explorer.tables", // same word
        "palette.kind.table", // same word
        "properties.kindTable", // same word
        "results.type.date", // same word
        "results.exportJsonDetail", // file extension
        "status.production", // environment badge
        "prod.header", // PRODUCTION badge with the connection and database names
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
        "menu.macServices", // macOS menu name, same word
        "er.tables", // TABLES · {0} / {1}, same word
        "er.title", // ER - {0}, same text
        "settings.update.channel.stable", // proper name
        "about.version", // same word
        "about.channel.stable", // same word
        "about.channel.beta", // proper name
        "about.runtime", // .NET version, not translated
        "about.runtime.jit", // .NET version, not translated
        "er.relationFrom", // only symbols and placeholders
        "plan.prop.workers", // engine term kept as is
        "tab.plan", // the name is the same word in both languages
        "plan.raw", // the name is the same word in both languages
        "plan.explainShortcut", // key chord
        "palette.section.actions", // the name is the same word in both languages
        "palette.mode.actions", // the name is the same word in both languages
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
