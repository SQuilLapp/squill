using Squill.Platform.Storage;
using Xunit;

namespace Squill.Platform.Tests;

public sealed class StorageStoresTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "squill-tests-" + Guid.NewGuid().ToString("N"));
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private JsonQueryHistoryStore NewHistory() => new(new AppPaths(_dir));

    private JsonSnippetStore NewSnippets() => new(new AppPaths(_dir));

    private static HistoryEntry Entry(int i) =>
        new(DateTimeOffset.UnixEpoch.AddSeconds(i), Guid.Empty, "select " + i, true, i, TimeSpan.FromMilliseconds(i));

    [Fact]
    public async Task AppendAsync_ThenList_ReturnsNewestLastAndRoundTrips()
    {
        var store = NewHistory();
        await store.AppendAsync(Entry(1), _ct);
        await store.AppendAsync(Entry(2), _ct);

        Assert.Equal([Entry(1), Entry(2)], await NewHistory().ListAsync(10, _ct));
        Assert.Equal([Entry(2)], await store.ListAsync(1, _ct));
    }

    [Fact]
    public async Task AppendAsync_BeyondCapacity_DropsOldest()
    {
        var store = NewHistory();
        var seed = Enumerable.Range(0, JsonQueryHistoryStore.Capacity).Select(Entry).ToList();
        await File.WriteAllTextAsync(
            Path.Combine(_dir, "history.json"),
            System.Text.Json.JsonSerializer.Serialize(seed, StorageJsonContext.Default.ListHistoryEntry),
            _ct);
        for (var i = JsonQueryHistoryStore.Capacity; i < JsonQueryHistoryStore.Capacity + 3; i++)
        {
            await store.AppendAsync(Entry(i), _ct);
        }

        var all = await store.ListAsync(int.MaxValue, _ct);
        Assert.Equal(JsonQueryHistoryStore.Capacity, all.Count);
        Assert.Equal(Entry(3), all[0]);
        Assert.Equal(Entry(JsonQueryHistoryStore.Capacity + 2), all[^1]);
    }

    [Fact]
    public async Task ClearAsync_RemovesEverything()
    {
        var store = NewHistory();
        await store.AppendAsync(Entry(1), _ct);
        await store.ClearAsync(_ct);

        Assert.Empty(await store.ListAsync(10, _ct));
    }

    [Fact]
    public async Task ListAsync_CorruptHistoryFile_RenamesItAndReturnsEmpty()
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(Path.Combine(_dir, "history.json"), "{ not json", _ct);

        Assert.Empty(await NewHistory().ListAsync(10, _ct));
        Assert.True(File.Exists(Path.Combine(_dir, "history.json.bad")));
    }

    [Fact]
    public async Task SnippetStore_SaveReplaceDelete_Works()
    {
        var store = NewSnippets();
        var snippet = new Snippet(Guid.NewGuid(), "a", "select 1");
        await store.SaveAsync(snippet, _ct);
        await store.SaveAsync(snippet with { Name = "b" }, _ct);

        Assert.Equal("b", Assert.Single(await NewSnippets().ListAsync(_ct)).Name);

        await store.DeleteAsync(Guid.NewGuid(), _ct);
        Assert.Single(await store.ListAsync(_ct));
        await store.DeleteAsync(snippet.Id, _ct);
        Assert.Empty(await store.ListAsync(_ct));
    }

    [Fact]
    public async Task ListAsync_CorruptSnippetFile_RenamesItAndReturnsEmpty()
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(Path.Combine(_dir, "snippets.json"), "[1,", _ct);

        Assert.Empty(await NewSnippets().ListAsync(_ct));
        Assert.True(File.Exists(Path.Combine(_dir, "snippets.json.bad")));
    }
}
