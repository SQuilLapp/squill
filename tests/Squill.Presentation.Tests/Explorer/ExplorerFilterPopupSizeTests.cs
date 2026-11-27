using Squill.Presentation.Explorer;
using Xunit;

namespace Squill.Presentation.Tests.Explorer;

public sealed class ExplorerFilterPopupSizeTests
{
    [Fact]
    public void Fit_WithinLimits_KeepsTheSize() =>
        Assert.Equal(new ExplorerFilterPopupSize(500, 400), ExplorerFilterPopupSize.Fit(new(500, 400), 1000, 1000));

    [Fact]
    public void Fit_TooLarge_ClampsToTheSpace() =>
        Assert.Equal(new ExplorerFilterPopupSize(700, 300), ExplorerFilterPopupSize.Fit(new(900, 800), 700, 300));

    [Fact]
    public void Fit_TooSmall_ClampsToTheMinimum() =>
        Assert.Equal(ExplorerFilterPopupSize.Minimum, ExplorerFilterPopupSize.Fit(new(10, 10), 1000, 1000));

    [Fact]
    public void Fit_WindowSmallerThanMinimum_TheSpaceWins() =>
        Assert.Equal(new ExplorerFilterPopupSize(200, 100), ExplorerFilterPopupSize.Fit(new(440, 520), 200, 100));

    [Fact]
    public void Fit_NegativeSpace_GivesZero() =>
        Assert.Equal(new ExplorerFilterPopupSize(0, 0), ExplorerFilterPopupSize.Fit(new(440, 520), -5, -1));

    [Fact]
    public void OpensAbove_FitsBelow_False() => Assert.False(ExplorerFilterPopupSize.OpensAbove(300, 400, 900));

    [Fact]
    public void OpensAbove_MoreRoomAbove_True() => Assert.True(ExplorerFilterPopupSize.OpensAbove(500, 200, 400));

    [Fact]
    public void OpensAbove_MoreRoomBelow_False() => Assert.False(ExplorerFilterPopupSize.OpensAbove(500, 300, 200));
}
