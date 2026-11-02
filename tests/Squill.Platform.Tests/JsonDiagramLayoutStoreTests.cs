using Squill.Platform.Storage;
using Xunit;

namespace Squill.Platform.Tests;

public sealed class JsonDiagramLayoutStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "squill-tests-" + Guid.NewGuid().ToString("N"));
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly Guid _a = Guid.NewGuid();
    private readonly Guid _b = Guid.NewGuid();

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private JsonDiagramLayoutStore NewStore() => new(new AppPaths(_dir));

    private static DiagramLayout Layout(double x, double zoom = 1.5) =>
        new(new Dictionary<string, DiagramPosition> { ["public.users"] = new(x, 20) }, zoom, 3, 4);

    [Fact]
    public async Task SaveAsync_ThenLoad_RoundTripsWithCaseInsensitiveKeys()
    {
        await NewStore().SaveAsync(_a, Layout(10), _ct);

        var loaded = await NewStore().LoadAsync(_a, _ct);

        Assert.NotNull(loaded);
        Assert.Equal(new DiagramPosition(10, 20), loaded.Positions["PUBLIC.USERS"]);
        Assert.Equal((1.5, 3, 4), (loaded.Zoom, loaded.OffsetX, loaded.OffsetY));
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ReturnsNull() => Assert.Null(await NewStore().LoadAsync(_a, _ct));

    [Fact]
    public async Task LoadAsync_CorruptFile_ReturnsNull()
    {
        var path = new AppPaths(_dir).Combine("diagrams", $"{_a:N}.json");
        await File.WriteAllTextAsync(path, "{ not json", _ct);

        Assert.Null(await NewStore().LoadAsync(_a, _ct));
    }

    [Fact]
    public async Task SaveAsync_Twice_Overwrites()
    {
        var store = NewStore();
        await store.SaveAsync(_a, Layout(10), _ct);
        await store.SaveAsync(_a, Layout(99, 2), _ct);

        var loaded = await store.LoadAsync(_a, _ct);

        Assert.Equal(99, loaded!.Positions["public.users"].X);
        Assert.Equal(2, loaded.Zoom);
    }

    [Fact]
    public async Task DeleteAsync_RemovesLayout_AndMissingIsNoOp()
    {
        var store = NewStore();
        await store.DeleteAsync(_a, _ct);
        await store.SaveAsync(_a, Layout(10), _ct);
        await store.DeleteAsync(_a, _ct);

        Assert.Null(await store.LoadAsync(_a, _ct));
    }

    [Fact]
    public async Task TwoProfiles_DoNotMix()
    {
        var store = NewStore();
        await store.SaveAsync(_a, Layout(1), _ct);
        await store.SaveAsync(_b, Layout(2), _ct);

        Assert.Equal(1, (await store.LoadAsync(_a, _ct))!.Positions["public.users"].X);
        Assert.Equal(2, (await store.LoadAsync(_b, _ct))!.Positions["public.users"].X);
    }
}
