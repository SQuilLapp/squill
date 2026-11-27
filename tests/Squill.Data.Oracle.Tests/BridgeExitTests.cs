using Xunit;

namespace Squill.Data.Oracle.Tests;

public sealed class BridgeExitTests
{
    [Fact]
    public async Task ABridgeThatExitsAtOnceExplainsWhyWithItsErrorOutput()
    {
        await using var client = new BridgeClient(new MemoryStream(), new MemoryStream(), _ => { }) { ExitDetail = () => "You must install or update .NET" };

        var ex = await Assert.ThrowsAsync<OracleBridgeException>(async () => await client.SendAsync(new BridgeRequest(0, "open"), CancellationToken.None));

        Assert.Contains("needs the .NET runtime", ex.Message, StringComparison.Ordinal);
        Assert.Contains("You must install or update .NET", ex.Message, StringComparison.Ordinal);
    }
}
