using Squill.Platform.Lifecycle;
using Xunit;

namespace Squill.Platform.Tests;

public sealed class SingleInstanceTests
{
    [Fact]
    public async Task SecondStart_IsRefused_AndAsksTheFirstToShowItself()
    {
        var name = "SQuilL-test-" + Guid.NewGuid().ToString("N");
        var activated = new TaskCompletionSource();
        using var first = SingleInstance.TryAcquire(name, () => activated.TrySetResult());

        Assert.NotNull(first);
        Assert.Null(SingleInstance.TryAcquire(name, () => { }));
        await activated.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }
}
