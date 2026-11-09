using Squill.Presentation.Updates;
using Squill.Platform.Updates;
using Xunit;

namespace Squill.Presentation.Tests.Updates;

public class UpdateBannerLayoutTests
{
    [Theory]
    [InlineData(UpdateStatus.Available, BannerParts.Details | BannerParts.Download)]
    [InlineData(UpdateStatus.Downloading, BannerParts.Details | BannerParts.Progress)]
    [InlineData(UpdateStatus.Ready, BannerParts.Details | BannerParts.Restart)]
    [InlineData(UpdateStatus.Failed, BannerParts.Details | BannerParts.Retry)]
    [InlineData(UpdateStatus.NotInstalled, BannerParts.None)]
    [InlineData(UpdateStatus.Idle, BannerParts.None)]
    [InlineData(UpdateStatus.Checking, BannerParts.None)]
    [InlineData(UpdateStatus.UpToDate, BannerParts.None)]
    public void Each_status_shows_its_own_controls(UpdateStatus status, BannerParts expected) =>
        Assert.Equal(expected, UpdateBannerLayout.PartsFor(status));

    [Fact]
    public void Every_status_is_covered_and_only_failure_warns()
    {
        foreach (var status in Enum.GetValues<UpdateStatus>())
            Assert.Equal(status == UpdateStatus.Failed, UpdateBannerLayout.IsWarning(status));
        Assert.Equal(8, Enum.GetValues<UpdateStatus>().Length);
    }
}
