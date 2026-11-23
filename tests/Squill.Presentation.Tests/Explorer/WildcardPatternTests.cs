using Squill.Presentation.Explorer;
using Xunit;

namespace Squill.Presentation.Tests.Explorer;

public sealed class WildcardPatternTests
{
    [Theory]
    [InlineData("sodda_*", "sodda_web", true)]
    [InlineData("sodda_*", "SODDA_WORKER", true)]
    [InlineData("sodda_*", "gate", false)]
    [InlineData("*", "", true)]
    [InlineData("*", "anything", true)]
    [InlineData("a?c", "abc", true)]
    [InlineData("a?c", "ac", false)]
    [InlineData("a?c", "abbc", false)]
    [InlineData("public.tmp_*", "public.tmp_x", true)]
    [InlineData("public.tmp_*", "ventes.tmp_x", false)]
    [InlineData("a.c", "abc", false)]
    [InlineData("a.c", "a.c", true)]
    [InlineData("[a]", "[a]", true)]
    [InlineData("[a]", "a", false)]
    [InlineData("ab*", "ab", true)]
    [InlineData("ab*", "a", false)]
    [InlineData("a*c", "abcbc", true)]
    [InlineData("a*c", "abcb", false)]
    [InlineData("**", "x", true)]
    [InlineData("a**b", "ab", true)]
    public void IsMatch_FollowsWildcardRules(string pattern, string name, bool expected) =>
        Assert.Equal(expected, WildcardPattern.IsMatch(pattern, name));

    [Theory]
    [InlineData("abc", false)]
    [InlineData("a*", true)]
    [InlineData("a?", true)]
    public void HasWildcard_DetectsStarAndQuestionMark(string pattern, bool expected) =>
        Assert.Equal(expected, WildcardPattern.HasWildcard(pattern));
}
