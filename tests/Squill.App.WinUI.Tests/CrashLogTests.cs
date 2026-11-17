using Squill.App.WinUI;
using Xunit;

namespace Squill.App.WinUI.Tests;

public class CrashLogTests
{
    [Fact]
    public void Format_writes_time_source_and_every_inner_exception()
    {
        var thrown = Throw(new InvalidOperationException("outer", new KeyNotFoundException("inner")));
        var text = CrashLog.Format("Application.UnhandledException", thrown, "ignored", new DateTimeOffset(2026, 11, 17, 11, 33, 0, TimeSpan.Zero));
        Assert.StartsWith("2026-11-17T11:33:00.0000000+00:00 Application.UnhandledException", text);
        Assert.Contains("System.InvalidOperationException: outer", text);
        Assert.Contains("System.Collections.Generic.KeyNotFoundException: inner", text);
        Assert.Contains("Throw", text);
        Assert.DoesNotContain("ignored", text);
    }

    [Fact]
    public void Format_without_an_exception_keeps_the_message() =>
        Assert.Contains("native failure", CrashLog.Format("x", null, "native failure", DateTimeOffset.UnixEpoch));

    private static Exception Throw(Exception e)
    {
        try
        {
            throw e;
        }
        catch (Exception caught)
        {
            return caught;
        }
    }
}
