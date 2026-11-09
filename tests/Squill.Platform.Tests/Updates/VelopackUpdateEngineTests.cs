using System.Runtime.InteropServices;
using Squill.Core;
using Squill.Platform.Updates;
using Velopack.Locators;
using Velopack.Logging;
using Xunit;

namespace Squill.Platform.Tests.Updates;

public sealed class VelopackUpdateEngineTests : IDisposable
{
    private const string Repo = "https://github.com/squillapp/squill";
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "squill-tests-" + Guid.NewGuid().ToString("N"));
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private VelopackUpdateEngine Installed(string? feed)
    {
        Directory.CreateDirectory(_dir);
        var locator = new TestVelopackLocator("SQuilLTest", "1.0.0", _dir, new NullVelopackLogger());
        return new VelopackUpdateEngine(Repo, feed, Architecture.X64, locator);
    }

    [Fact]
    public async Task NotInstalled_ReportsItAndCheckThrowsNotInstalled()
    {
        var engine = new VelopackUpdateEngine(Repo, feedOverride: null);

        Assert.False(engine.IsInstalled);
        Assert.Null(engine.InstalledVersion);
        var ex = await Assert.ThrowsAsync<UpdateEngineException>(async () => await engine.CheckAsync(UpdateChannel.Stable, _ct));
        Assert.Equal(UpdateFailure.NotInstalled, ex.Kind);
    }

    [Fact]
    public void Installed_ReportsVersion()
    {
        var engine = Installed(feed: null);

        Assert.True(engine.IsInstalled);
        Assert.Equal(Ver.Of("1.0.0"), engine.InstalledVersion);
    }

    [Theory]
    [InlineData(UpdateChannel.Stable, Architecture.X64, "stable-x64")]
    [InlineData(UpdateChannel.Beta, Architecture.X64, "beta-x64")]
    [InlineData(UpdateChannel.Stable, Architecture.Arm64, "stable-arm64")]
    [InlineData(UpdateChannel.Beta, Architecture.Arm64, "beta-arm64")]
    public void ChannelName_CarriesTheArchitecture(UpdateChannel channel, Architecture architecture, string expected) =>
        Assert.Equal(expected, VelopackUpdateEngine.ChannelName(channel, architecture));

    [Fact]
    public void Classify_SeparatesTheFeedKinds()
    {
        Assert.Equal(VelopackUpdateEngine.FeedKind.Directory, VelopackUpdateEngine.Classify(Path.GetTempPath()));
        Assert.Equal(VelopackUpdateEngine.FeedKind.Web, VelopackUpdateEngine.Classify("http://127.0.0.1:1/"));
        Assert.Equal(VelopackUpdateEngine.FeedKind.Web, VelopackUpdateEngine.Classify("https://example.org/feed"));
        Assert.Equal(VelopackUpdateEngine.FeedKind.Invalid, VelopackUpdateEngine.Classify("ftp://example.org/feed"));
        Assert.Equal(VelopackUpdateEngine.FeedKind.Invalid, VelopackUpdateEngine.Classify(Path.Combine(_dir, "missing")));
        Assert.Equal(VelopackUpdateEngine.FeedKind.GitHub, VelopackUpdateEngine.Classify(null));
        Assert.Equal(VelopackUpdateEngine.FeedKind.GitHub, VelopackUpdateEngine.Classify("  "));
    }

    [Fact]
    public async Task Check_GarbageFeed_IsFeedInvalid()
    {
        var engine = Installed("not a feed");

        var ex = await Assert.ThrowsAsync<UpdateEngineException>(async () => await engine.CheckAsync(UpdateChannel.Stable, _ct));

        Assert.Equal(UpdateFailure.FeedInvalid, ex.Kind);
    }

    [Fact]
    public async Task Check_UnreachableFeed_IsNetworkFailure()
    {
        var engine = Installed("http://127.0.0.1:1/");

        var ex = await Assert.ThrowsAsync<UpdateEngineException>(async () => await engine.CheckAsync(UpdateChannel.Stable, _ct));

        Assert.Equal(UpdateFailure.Network, ex.Kind);
    }

    [Fact]
    public async Task Check_HonorsCancellation()
    {
        var engine = Installed("http://127.0.0.1:1/");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await engine.CheckAsync(UpdateChannel.Stable, cts.Token));
    }

    [Fact]
    public void Download_ForeignOffer_IsRejected()
    {
        var engine = Installed(feed: null);
        var offer = new UpdateOffer(Ver.Of("2.0.0"), null, 0, IsDelta: false, new object());

        Assert.Throws<ArgumentException>(() => engine.ApplyAndRestart(offer));
    }

    [Fact]
    public void Factory_ReturnsAnEngine() =>
        Assert.IsType<VelopackUpdateEngine>(UpdateEngineFactory.Create(Repo, feedOverride: null));
}
