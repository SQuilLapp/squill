using Xunit;

namespace Squill.Core.Tests;

public sealed class TextRangeTests
{
    [Fact]
    public void End_StartAndLength_ReturnsSum() => Assert.Equal(7, new TextRange(3, 4).End);
}
