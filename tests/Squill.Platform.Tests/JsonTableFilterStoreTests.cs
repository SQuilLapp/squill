using Squill.Platform.Storage;
using Xunit;

namespace Squill.Platform.Tests;

public sealed class JsonTableFilterStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "squill-tests-" + Guid.NewGuid().ToString("N"));
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly Guid _profile = Guid.NewGuid();

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private JsonTableFilterStore NewStore() => new(new AppPaths(_dir));

    [Fact]
    public async Task SaveAsync_ThenLoad_RoundTripsPerDatabaseWithCaseInsensitiveDatabaseNames()
    {
        var filter = new TableFilter(new Dictionary<string, IReadOnlyList<string>> { ["App"] = ["public.logs", "public.tmp"], [""] = ["t"] });
        await NewStore().SaveAsync(_profile, filter, _ct);

        var loaded = await NewStore().LoadAsync(_profile, _ct);

        Assert.NotNull(loaded);
        Assert.Equal(["public.logs", "public.tmp"], loaded.Hidden["app"]);
        Assert.Equal(["t"], loaded.Hidden[""]);
    }

    [Fact]
    public async Task SaveAsync_ThenLoad_RoundTripsAllFourParts_IgnoringCase()
    {
        var filter = new TableFilter(new Dictionary<string, IReadOnlyList<string>> { ["App"] = ["public.logs"] })
        {
            Databases = new NameFilter(["old"], ["sodda_*"]),
            Schemas = new Dictionary<string, NameFilter> { ["App"] = new NameFilter(["tmp"], []) },
            TablePatterns = new Dictionary<string, IReadOnlyList<string>> { ["App"] = ["public.tmp_*"] },
        };
        await NewStore().SaveAsync(_profile, filter, _ct);

        var loaded = await NewStore().LoadAsync(_profile, _ct);

        Assert.NotNull(loaded);
        Assert.Equal(["old"], loaded.Databases!.Hidden);
        Assert.Equal(["sodda_*"], loaded.Databases.Patterns);
        Assert.Equal(["tmp"], loaded.Schemas["app"].Hidden);
        Assert.Equal(["public.tmp_*"], loaded.TablePatterns["APP"]);
        Assert.Equal(["public.logs"], loaded.Hidden["aPP"]);
    }

    [Fact]
    public async Task LoadAsync_OldFile_HasEmptyNewParts()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "table-filters"));
        await File.WriteAllTextAsync(Path.Combine(_dir, "table-filters", $"{_profile:N}.json"), "{\"hidden\":{\"App\":[\"t\"]}}", _ct);

        var loaded = await NewStore().LoadAsync(_profile, _ct);

        Assert.NotNull(loaded);
        Assert.Null(loaded.Databases);
        Assert.Empty(loaded.Schemas);
        Assert.Empty(loaded.TablePatterns);
        Assert.Equal(["t"], loaded.Hidden["app"]);
    }

    [Fact]
    public async Task LoadAsync_MissingOrCorruptFile_ReturnsNull()
    {
        Assert.Null(await NewStore().LoadAsync(_profile, _ct));
        Directory.CreateDirectory(Path.Combine(_dir, "table-filters"));
        await File.WriteAllTextAsync(Path.Combine(_dir, "table-filters", $"{_profile:N}.json"), "{ nope", _ct);
        Assert.Null(await NewStore().LoadAsync(_profile, _ct));
    }
}
