using Squill.Core;
using Squill.Presentation.Localization;
using Squill.Sql;
using Xunit;

namespace Squill.Presentation.Tests.Localization;

public sealed class IssueTextTests
{
    private static IEnumerable<string> AllCodes => [.. CoreMessageCodes.All, .. SqlMessageCodes.All];

    [Fact]
    public void EveryCode_HasAnIssueKeyInBothCatalogs()
    {
        // A countable code has a .one / .other pair instead of a single key.
        foreach (var entries in new[] { UiTextCatalogFr.Entries, UiTextCatalogEn.Entries })
        {
            var keys = entries.Select(e => e.Key).ToHashSet();
            var missing = AllCodes.Where(c => !keys.Contains("issue." + c) && !keys.Contains($"issue.{c}.other"));
            Assert.Empty(missing);
        }
    }

    [Fact]
    public void EveryIssueKey_BelongsToACode()
    {
        var codes = AllCodes.ToHashSet();
        var orphans = UiTextCatalogEn.Entries.Select(e => e.Key)
            .Where(k => k.StartsWith("issue.", StringComparison.Ordinal))
            .Select(k => k["issue.".Length..])
            .Select(k => k.EndsWith(".one", StringComparison.Ordinal) ? k[..^4] : k.EndsWith(".other", StringComparison.Ordinal) ? k[..^6] : k)
            .Where(k => !codes.Contains(k));
        Assert.Empty(orphans);
    }

    [Theory]
    [InlineData(UiLanguage.French, "La colonne 'x' n'a pas de type de données.")]
    [InlineData(UiLanguage.English, "The column 'x' has no data type.")]
    public void Format_FillsTheCatalogText(UiLanguage language, string expected)
    {
        using var _ = UiText.Override(language);
        Assert.Equal(expected, IssueText.Format(CoreMessageCodes.DesignColumnTypeMissing, ["x"], "fallback"));
    }

    [Theory]
    [InlineData(UiLanguage.French, "0", "**table** t (0 colonne)")]
    [InlineData(UiLanguage.French, "3", "**table** t (3 colonnes)")]
    [InlineData(UiLanguage.English, "1", "**table** t (1 column)")]
    [InlineData(UiLanguage.English, "3", "**table** t (3 columns)")]
    public void Format_ChoosesThePluralFormByTheFirstArgument(UiLanguage language, string count, string expected)
    {
        using var _ = UiText.Override(language);
        Assert.Equal(expected, IssueText.Format(SqlMessageCodes.HoverTable, [count, "t"], "fallback"));
    }

    [Fact]
    public void Format_WithoutKnownCode_ReturnsTheEnglishMessage()
    {
        using var _ = UiText.Override(UiLanguage.French);
        Assert.Equal("english", IssueText.Format(null, null, "english"));
        Assert.Equal("english", IssueText.Format("no.such.code", ["a"], "english"));
    }
}
