using System.Globalization;
using Xunit;

namespace Squill.Core.Tests;

public sealed class SemanticVersionTests
{
    private static SemanticVersion V(string s) => SemanticVersion.Parse(s, CultureInfo.InvariantCulture);

    private static readonly string[] Chain =
    [
        "1.0.0-alpha", "1.0.0-alpha.1", "1.0.0-alpha.beta", "1.0.0-beta", "1.0.0-beta.2", "1.0.0-beta.11", "1.0.0-rc.1", "1.0.0",
    ];

    [Fact]
    public void Precedence_FollowsSemVerChain()
    {
        for (var i = 0; i < Chain.Length - 1; i++)
        {
            var lower = V(Chain[i]);
            var higher = V(Chain[i + 1]);
            Assert.True(lower < higher, $"{lower} < {higher}");
            Assert.True(lower <= higher);
            Assert.True(higher > lower);
            Assert.True(higher >= lower);
            Assert.True(lower.CompareTo(higher) < 0);
            Assert.True(higher.CompareTo(lower) > 0);
        }
    }

    [Theory]
    [InlineData("1.2.10", "1.2.9")]
    [InlineData("1.10.0", "1.9.9")]
    [InlineData("2.0.0", "1.99.99")]
    [InlineData("1.0.0-2", "1.0.0-1")]
    [InlineData("1.0.0-alpha.10", "1.0.0-alpha.9")]
    [InlineData("1.0.0-alpha.100", "1.0.0-alpha.99")]
    public void Precedence_IsNumeric(string higher, string lower) =>
        Assert.True(V(higher) > V(lower));

    [Fact]
    public void Compare_EqualVersions_IsZeroAndMetadataIsIgnored()
    {
        var a = V("1.0.0-rc.1+build.5");
        var b = V("1.0.0-rc.1");
        Assert.Equal(0, a.CompareTo(b));
        Assert.Equal(a, b);
        Assert.True(a <= b && a >= b);
    }

    [Theory]
    [InlineData("0.1.0", 0, 1, 0, "")]
    [InlineData("1.2.3", 1, 2, 3, "")]
    [InlineData("1.2.3-beta.1", 1, 2, 3, "beta.1")]
    [InlineData("1.0.0-0.3.7", 1, 0, 0, "0.3.7")]
    [InlineData("1.0.0-x-y-z.--", 1, 0, 0, "x-y-z.--")]
    [InlineData("1.0.0+abc", 1, 0, 0, "")]
    [InlineData("1.0.0+001", 1, 0, 0, "")]
    [InlineData("1.0.0-rc.1+build.5", 1, 0, 0, "rc.1")]
    public void Parse_Valid(string text, int major, int minor, int patch, string pre)
    {
        var v = V(text);
        Assert.Equal(new SemanticVersion(major, minor, patch, pre), v);
        Assert.Equal(pre.Length > 0, v.IsPreRelease);
        Assert.True(SemanticVersion.TryParse(text, null, out var viaProvider));
        Assert.Equal(v, viaProvider);
        Assert.Equal(v, SemanticVersion.Parse(text, null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("v1.0.0")]
    [InlineData("1.0")]
    [InlineData("1")]
    [InlineData("1.0.")]
    [InlineData("1..0")]
    [InlineData("1.0.0.0")]
    [InlineData("01.0.0")]
    [InlineData("1.00.0")]
    [InlineData("1.0.00")]
    [InlineData("-1.0.0")]
    [InlineData("a.b.c")]
    [InlineData("99999999999.0.0")]
    [InlineData("1.0.0-")]
    [InlineData("1.0.0-01")]
    [InlineData("1.0.0-a..b")]
    [InlineData("1.0.0-a_b")]
    [InlineData("1.0.0-é")]
    [InlineData("1.0.0+")]
    [InlineData("1.0.0+a..b")]
    [InlineData("1.0.0+a_b")]
    [InlineData("garbage")]
    public void TryParse_Invalid_ReturnsFalse(string text)
    {
        Assert.False(SemanticVersion.TryParse(text, out var v));
        Assert.Equal(default, v);
        Assert.False(SemanticVersion.TryParse(text, null, out _));
        Assert.Throws<FormatException>(() => V(text));
    }

    [Fact]
    public void TryParse_Null_ReturnsFalse() => Assert.False(SemanticVersion.TryParse(null, out _));

    [Theory]
    [InlineData("1.2.3")]
    [InlineData("1.2.3-beta.1")]
    [InlineData("0.1.0-rc.1.x-y")]
    public void ToString_RoundTrips(string text) => Assert.Equal(text, V(text).ToString());

    [Fact]
    public void Default_ToStringAndPrecedenceDoNotThrow()
    {
        Assert.Equal("0.0.0", default(SemanticVersion).ToString());
        Assert.True(default(SemanticVersion) < V("0.0.1"));
    }

    [Theory]
    [InlineData("1.0.0", UpdateChannel.Stable, "stable")]
    [InlineData("1.0.0-beta.1", UpdateChannel.Beta, "beta")]
    public void Channel_FollowsPreRelease(string text, UpdateChannel channel, string id)
    {
        Assert.Equal(channel, V(text).ChannelOf());
        Assert.Equal(id, channel.ChannelId());
    }

    [Fact]
    public void ChannelId_UndefinedValue_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ((UpdateChannel)42).ChannelId());
}
