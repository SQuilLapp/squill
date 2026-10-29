using Xunit;
using Squill.Platform.Secrets;

namespace Squill.Platform.Tests;

public sealed class InMemorySecretStoreTests
{
    [Fact]
    public async Task GetAsync_AfterSet_ReturnsLatestSecret()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemorySecretStore();
        await store.SetAsync("k", "s1", ct);
        await store.SetAsync("k", "s2", ct);

        Assert.Equal("s2", await store.GetAsync("k", ct));
    }

    [Fact]
    public async Task GetAsync_UnknownKey_ReturnsNull()
    {
        Assert.Null(await new InMemorySecretStore().GetAsync("missing", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_MissingKey_IsNoOp()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = new InMemorySecretStore();
        await store.DeleteAsync("missing", ct);
        await store.SetAsync("k", "s", ct);
        await store.DeleteAsync("k", ct);

        Assert.Null(await store.GetAsync("k", ct));
    }
}
