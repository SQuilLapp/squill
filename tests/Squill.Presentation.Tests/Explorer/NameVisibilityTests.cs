using Squill.Presentation.Explorer;
using Xunit;

namespace Squill.Presentation.Tests.Explorer;

public sealed class NameVisibilityTests
{
    [Fact]
    public void Default_IsOptOut_NewNamesAreVisible()
    {
        var v = new NameVisibility();
        Assert.True(v.ShowsEverything);
        Assert.True(v.IsVisible("new"));
    }

    [Fact]
    public void HideThenShow_RoundTrips()
    {
        var v = new NameVisibility();
        v.Hide("a");
        Assert.False(v.IsVisible("A"));
        Assert.False(v.ShowsEverything);
        v.Show("a");
        Assert.True(v.IsVisible("a"));
        Assert.True(v.ShowsEverything);
    }

    [Fact]
    public void FirstPattern_HidesNonMatchingNames()
    {
        var v = new NameVisibility();
        Assert.True(v.AddPattern("sodda_*"));
        Assert.True(v.IsVisible("sodda_web"));
        Assert.False(v.IsVisible("gate"));
    }

    [Fact]
    public void Show_InPatternMode_AddsExactPattern_AndHideRemovesIt()
    {
        var v = new NameVisibility(patterns: ["sodda_*"]);
        v.Show("gate");
        Assert.True(v.IsVisible("gate"));
        Assert.Contains("gate", v.Patterns);
        v.Hide("gate");
        Assert.False(v.IsVisible("gate"));
        Assert.DoesNotContain("gate", v.Patterns);
        Assert.Empty(v.Hidden);
    }

    [Fact]
    public void Hide_NameMatchedByWildcard_AddsToHidden()
    {
        var v = new NameVisibility(patterns: ["sodda_*"]);
        v.Hide("sodda_web");
        Assert.False(v.IsVisible("sodda_web"));
        Assert.Contains("sodda_web", v.Hidden);
    }

    [Fact]
    public void HideAll_AndAllVisible()
    {
        var v = new NameVisibility();
        Assert.True(v.AllVisible(["a", "b"]));
        v.HideAll(["a", "b"]);
        Assert.False(v.IsVisible("a"));
        Assert.False(v.IsVisible("b"));
        Assert.False(v.AllVisible(["a", "b"]));
    }

    [Fact]
    public void ShowAll_WithoutPrefix_ResetsToShowsEverything()
    {
        var v = new NameVisibility(["a"], ["x*"]);
        v.ShowAll(["a"]);
        Assert.True(v.ShowsEverything);
    }

    [Fact]
    public void ShowAll_WithPrefix_RemovesOnlyScopePatterns_AndKeepsScopeShown()
    {
        var v = new NameVisibility(["public.t"], ["public.tmp_*", "ventes.*"]);
        v.ShowAll(["public.t"], "public.");
        Assert.Equal(["ventes.*", "public.*"], v.Patterns);
        Assert.True(v.IsVisible("public.anything"));
        Assert.Empty(v.Hidden);
    }

    [Fact]
    public void ShowAll_WithPrefix_NoPatternLeft_ShowsEverything()
    {
        var v = new NameVisibility(patterns: ["public.tmp_*"]);
        v.ShowAll([], "public.");
        Assert.True(v.ShowsEverything);
    }

    [Fact]
    public void AddPattern_RefusesBlankAndDuplicates()
    {
        var v = new NameVisibility();
        Assert.False(v.AddPattern("  "));
        Assert.True(v.AddPattern(" a* "));
        Assert.False(v.AddPattern("A*"));
        Assert.Equal(["a*"], v.Patterns);
        v.RemovePattern("A*");
        Assert.Empty(v.Patterns);
    }

    [Fact]
    public void ToSaved_IsNullWhenEverythingShown_SortedOtherwise()
    {
        Assert.Null(new NameVisibility().ToSaved());
        var v = new NameVisibility(["b", "A", "c"], ["z*", "m*"]);
        var saved = v.ToSaved();
        Assert.NotNull(saved);
        Assert.Equal(["A", "b", "c"], saved.Hidden);
        Assert.Equal(["z*", "m*"], saved.Patterns);
    }
}
