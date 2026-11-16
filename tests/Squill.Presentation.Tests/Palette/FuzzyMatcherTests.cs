using System.Diagnostics;
using Squill.Presentation.Palette;
using Xunit;

namespace Squill.Presentation.Tests.Palette;

public sealed class FuzzyMatcherTests
{
    private static int Score(string candidate, string pattern)
    {
        FuzzyMatch? m = FuzzyMatcher.Match(candidate, pattern);
        Assert.NotNull(m);
        return m.Score;
    }

    [Theory]
    [InlineData("commandes")]
    [InlineData("lignes_commande")]
    [InlineData("v_commandes_recentes")]
    [InlineData("Commit — valider la transaction")]
    [InlineData("Commenter / décommenter la ligne")]
    [InlineData("Comparer les schémas...")]
    public void Match_M13Examples_Matches(string candidate) =>
        Assert.NotNull(FuzzyMatcher.Match(candidate, "comm"));

    [Fact]
    public void Match_PrefixCandidate_RangeCoversPattern()
    {
        FuzzyMatch? m = FuzzyMatcher.Match("commandes", "comm");
        Assert.NotNull(m);
        Assert.Equal([(0, 4)], m.Ranges);
    }

    [Fact]
    public void Match_ScatteredCandidate_RangesAreSplitRuns()
    {
        FuzzyMatch? m = FuzzyMatcher.Match("Comparer les schémas...", "comm");
        Assert.NotNull(m);
        Assert.Equal([(0, 3), (17, 1)], m.Ranges);
    }

    [Fact]
    public void Match_Ranking_ExactThenPrefixThenWordStartThenRunThenScattered()
    {
        int exact = Score("co", "co");
        int prefix = Score("code", "co");
        int wordStart = Score("x_co", "co");
        int run = Score("xco", "co");
        int scattered = Score("cxo", "co");
        Assert.True(exact > prefix);
        Assert.True(prefix > wordStart);
        Assert.True(wordStart > run);
        Assert.True(run > scattered);
    }

    [Fact]
    public void Match_Accents_RangesPointAtOriginal()
    {
        FuzzyMatch? m = FuzzyMatcher.Match("École", "ecole");
        Assert.NotNull(m);
        Assert.Equal([(0, 5)], m.Ranges);
        FuzzyMatch? d = FuzzyMatcher.Match("x École", "ecole");
        Assert.NotNull(d);
        Assert.Equal([(2, 6)], d.Ranges);
    }

    [Fact]
    public void Match_Case_Ignored() => Assert.NotNull(FuzzyMatcher.Match("SELECT", "sel"));

    [Fact]
    public void Match_Words_MustBeInOrder()
    {
        Assert.NotNull(FuzzyMatcher.Match("cmd table", "cmd tab"));
        Assert.Null(FuzzyMatcher.Match("cmd table", "tab cmd"));
    }

    [Fact]
    public void Match_EmptyPattern_ScoreZeroNoRanges()
    {
        FuzzyMatch? m = FuzzyMatcher.Match("abc", "  ");
        Assert.NotNull(m);
        Assert.Equal(0, m.Score);
        Assert.Empty(m.Ranges);
    }

    [Fact]
    public void Match_NoMatch_ReturnsNull()
    {
        Assert.Null(FuzzyMatcher.Match("abc", "abd"));
        Assert.Null(FuzzyMatcher.Match("", "a"));
    }

    [Fact]
    public void Match_LongCandidate_IsCutNotThrown()
    {
        string s = new string('a', 10_000) + "z";
        Assert.NotNull(FuzzyMatcher.Match(s, "aa"));
        Assert.Null(FuzzyMatcher.Match(s, "z"));
    }

    [Fact]
    public void Match_FiftyThousandNames_StaysFast()
    {
        string[] names = new string[50_000];
        for (int i = 0; i < names.Length; i++)
        {
            names[i] = $"table_{i:D6}_column_name_x";
        }

        Stopwatch sw = Stopwatch.StartNew();
        int hits = 0;
        foreach (string n in names)
        {
            if (FuzzyMatcher.Match(n, "tcnx") is not null)
            {
                hits++;
            }
        }

        Assert.Equal(names.Length, hits);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), sw.Elapsed.ToString());
    }
}
