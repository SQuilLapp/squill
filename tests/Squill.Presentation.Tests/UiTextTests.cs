using System.Text.RegularExpressions;
using Squill.Presentation.Localization;
using Xunit;

namespace Squill.Presentation.Tests;

public sealed partial class UiTextTests
{
    // UiText.Get("k") / UiText.Format("k", ...) in code, {loc:Text k} in Avalonia XAML, loc:Loc.Key="k" in WinUI XAML.
    [GeneratedRegex(@"UiText\.(?:Get|Format|FormatPlural)\(\s*""([^""]+)""|\{loc:Text\s+([\w.]+)\}|Loc\.Key=""([\w.]+)""")]
    private static partial Regex KeyUse();

    [Fact]
    public void Get_KnownKey_ReturnsFrench()
    {
        using var _ = UiText.Override(UiLanguage.French);
        Assert.Equal("EXPLORATEUR", UiText.Get("explorer.title"));
    }

    [Fact]
    public void Get_KnownKey_ReturnsEnglish()
    {
        using var _ = UiText.Override(UiLanguage.English);
        Assert.Equal("EXPLORER", UiText.Get("explorer.title"));
    }

    [Fact]
    public void Get_MissingKey_ReturnsMarker() => Assert.Equal("!no.such.key!", UiText.Get("no.such.key"));

    [Fact]
    public void Format_FillsPlaceholdersWithFrenchCulture()
    {
        using var _ = UiText.Override(UiLanguage.French);
        Assert.Equal("Lignes 1–1,5 sur 2 chargées", UiText.Format("results.footer", 1.5, 2));
    }

    [Fact]
    public void Format_FillsPlaceholdersWithEnglishCulture()
    {
        using var _ = UiText.Override(UiLanguage.English);
        Assert.Equal("Rows 1–1.5 of 2 loaded", UiText.Format("results.footer", 1.5, 2));
    }

    [Fact]
    public void Format_MissingKey_ReturnsMarker() => Assert.Equal("!no.such.key!", UiText.Format("no.such.key", 1));

    [Theory]
    [InlineData(UiLanguage.French, 0, "0 modification en attente")]
    [InlineData(UiLanguage.French, 1, "1 modification en attente")]
    [InlineData(UiLanguage.French, 3, "3 modifications en attente")]
    [InlineData(UiLanguage.English, 0, "0 pending changes")]
    [InlineData(UiLanguage.English, 1, "1 pending change")]
    [InlineData(UiLanguage.English, 3, "3 pending changes")]
    public void FormatPlural_FollowsTheLanguageRule(UiLanguage language, int count, string expected)
    {
        using var _ = UiText.Override(language);
        Assert.Equal(expected, UiText.FormatPlural("results.pendingChanges", count));
    }

    [Fact]
    public void FormatPlural_MissingKey_ReturnsMarker() => Assert.Equal("!no.such.other!", UiText.FormatPlural("no.such", 5));

    [Fact]
    public void Override_RestoresThePreviousLanguageAndExposesTheCulture()
    {
        using var french = UiText.Override(UiLanguage.French);
        using (UiText.Override(UiLanguage.English))
        {
            Assert.Equal(UiLanguage.English, UiText.Language);
            Assert.Equal("en-US", UiText.Culture.Name);
        }

        Assert.Equal(UiLanguage.French, UiText.Language);
        Assert.Equal("fr-FR", UiText.Culture.Name);
    }

    [Fact]
    public void Keys_AreUnique() =>
        Assert.Empty(UiTextCatalogFr.Entries.GroupBy(e => e.Key).Where(g => g.Count() > 1).Select(g => g.Key));

    [Fact]
    public void Values_UseStraightApostropheAndNoNonBreakingSpace() =>
        Assert.DoesNotContain(UiTextCatalogFr.Entries, e => e.Value.Contains('’') || e.Value.Contains(' ') || e.Value.Contains(' '));

    [Fact]
    public void EveryKeyUsedInSources_ExistsInCatalog()
    {
        var root = AppContext.BaseDirectory;
        while (root is not null && !Directory.Exists(Path.Combine(root, "src")))
            root = Path.GetDirectoryName(root);
        Assert.NotNull(root);

        var sep = Path.DirectorySeparatorChar;
        var known = UiTextCatalogFr.Entries.Select(e => e.Key).ToHashSet();
        // FormatPlural takes the base key of a .one / .other pair.
        known.UnionWith(known.Where(k => k.EndsWith(".one", StringComparison.Ordinal)).Select(k => k[..^4]).ToList());
        var missing = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories)
                     .Where(f => (f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith("axaml", StringComparison.Ordinal) || f.EndsWith(".xaml", StringComparison.Ordinal))
                                 && !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}")))
            foreach (Match m in KeyUse().Matches(File.ReadAllText(file)))
            {
                var key = m.Groups.Cast<Group>().Skip(1).First(g => g.Success).Value;
                if (!known.Contains(key))
                    missing.Add($"{Path.GetFileName(file)}: {key}");
            }

        Assert.Empty(missing);
    }
}
