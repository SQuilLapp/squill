using Squill.Presentation.Editing;
using Xunit;

namespace Squill.Presentation.Tests.Editing;

public sealed class LongPasteTests
{
    [Fact]
    public void IsLong_BelowThreshold_False() => Assert.False(LongPaste.IsLong(new string('a', LongPaste.Threshold - 1)));

    [Fact]
    public void IsLong_AtThreshold_True() => Assert.True(LongPaste.IsLong(new string('a', LongPaste.Threshold)));

    [Fact]
    public void IsLong_Null_False() => Assert.False(LongPaste.IsLong(null));
}
