using System.Text.RegularExpressions;
using Squill.Presentation.Localization;
using Xunit;

namespace Squill.Presentation.Tests;

public sealed partial class UiTextTests
{
    // UiText.Get("k") / UiText.Format("k", ...) in code, {loc:Text k} in Avalonia XAML, loc:Loc.Key="k" in WinUI XAML.
    [GeneratedRegex(@"UiText\.(?:Get|Format)\(\s*""([^""]+)""|\{loc:Text\s+([\w.]+)\}|Loc\.Key=""([\w.]+)""")]
    private static partial Regex KeyUse();

    [Fact]
    public void Get_KnownKey_ReturnsFrench() => Assert.Equal("EXPLORATEUR", UiText.Get("explorer.title"));

    [Fact]
    public void Get_MissingKey_ReturnsMarker() => Assert.Equal("!no.such.key!", UiText.Get("no.such.key"));

    [Fact]
    public void Format_FillsPlaceholdersWithFrenchCulture() =>
        Assert.Equal("Lignes 1–1,5 sur 2 chargées", UiText.Format("results.footer", 1.5, 2));

    [Fact]
    public void Format_MissingKey_ReturnsMarker() => Assert.Equal("!no.such.key!", UiText.Format("no.such.key", 1));

    [Fact]
    public void PluralKeys_ComeInPairs()
    {
        var keys = UiTextCatalog.Entries.Select(e => e.Key).ToHashSet();
        foreach (var one in keys.Where(k => k.EndsWith(".one", StringComparison.Ordinal)))
            Assert.Contains(one[..^4] + ".other", keys);
        Assert.Equal("1 modification en attente", UiText.Format("results.pendingChanges.one", 1));
        Assert.Equal("3 modifications en attente", UiText.Format("results.pendingChanges.other", 3));
    }

    [Fact]
    public void Keys_AreUnique() =>
        Assert.Empty(UiTextCatalog.Entries.GroupBy(e => e.Key).Where(g => g.Count() > 1).Select(g => g.Key));

    [Fact]
    public void Values_UseStraightApostropheAndNoNonBreakingSpace() =>
        Assert.DoesNotContain(UiTextCatalog.Entries, e => e.Value.Contains('’') || e.Value.Contains(' ') || e.Value.Contains(' '));

    [Fact]
    public void EveryKeyUsedInSources_ExistsInCatalog()
    {
        var root = AppContext.BaseDirectory;
        while (root is not null && !Directory.Exists(Path.Combine(root, "src")))
            root = Path.GetDirectoryName(root);
        Assert.NotNull(root);

        var sep = Path.DirectorySeparatorChar;
        var known = UiTextCatalog.Entries.Select(e => e.Key).ToHashSet();
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
