using Squill.Platform.Profiles;
using Xunit;

namespace Squill.Platform.Tests;

public sealed class JsonRecentConnectionsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "squill-tests-" + Guid.NewGuid().ToString("N"));
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private JsonRecentConnectionsStore NewStore() => new(new AppPaths(_dir));

    [Fact]
    public async Task TouchAsync_OrdersNewestFirst()
    {
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        using var store = NewStore();
        await store.TouchAsync(a, T0, _ct);
        await store.TouchAsync(b, T0.AddMinutes(1), _ct);

        Assert.Equal([b, a], (await store.ListAsync(_ct)).Select(r => r.ProfileId));
    }

    [Fact]
    public async Task TouchAsync_Twice_KeepsOneEntryAtTheFront()
    {
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        using var store = NewStore();
        await store.TouchAsync(a, T0, _ct);
        await store.TouchAsync(b, T0.AddMinutes(1), _ct);
        await store.TouchAsync(a, T0.AddMinutes(2), _ct);

        var list = await store.ListAsync(_ct);
        Assert.Equal([a, b], list.Select(r => r.ProfileId));
        Assert.Equal(T0.AddMinutes(2), list[0].LastOpened);
    }

    [Fact]
    public async Task TouchAsync_21stEntry_DropsTheOldest()
    {
        var ids = Enumerable.Range(0, 21).Select(_ => Guid.NewGuid()).ToList();
        using var store = NewStore();
        for (var i = 0; i < ids.Count; i++)
        {
            await store.TouchAsync(ids[i], T0.AddMinutes(i), _ct);
        }

        var list = await store.ListAsync(_ct);
        Assert.Equal(20, list.Count);
        Assert.DoesNotContain(list, r => r.ProfileId == ids[0]);
        Assert.Equal(ids[20], list[0].ProfileId);
    }

    [Fact]
    public async Task ForgetAsync_RemovesTheEntry()
    {
        var a = Guid.NewGuid();
        using var store = NewStore();
        await store.TouchAsync(a, T0, _ct);
        await store.ForgetAsync(a, _ct);
        await store.ForgetAsync(Guid.NewGuid(), _ct);

        Assert.Empty(await store.ListAsync(_ct));
    }

    [Fact]
    public async Task ListAsync_CorruptFile_ReadsAsEmptyAndIsRewrittenOnTouch()
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(Path.Combine(_dir, "recent.json"), "{not json", _ct);
        var a = Guid.NewGuid();
        using var store = NewStore();

        Assert.Empty(await store.ListAsync(_ct));
        await store.TouchAsync(a, T0, _ct);

        Assert.Equal([a], (await store.ListAsync(_ct)).Select(r => r.ProfileId));
    }
}
