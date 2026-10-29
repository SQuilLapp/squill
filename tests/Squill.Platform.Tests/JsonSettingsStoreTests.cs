using Squill.Core;
using Squill.Platform.Settings;
using Xunit;

namespace Squill.Platform.Tests;

public sealed class JsonSettingsStoreTests : IDisposable
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

    private JsonSettingsStore NewStore() => new(new AppPaths(_dir));

    [Fact]
    public async Task LoadAsync_MissingFile_ReturnsDefaults()
    {
        Assert.Equal(AppSettings.Default, await NewStore().LoadAsync(_ct));
        Assert.Equal(new AppSettings(ThemeMode.System, 13, 1_000_000, KeywordCase.Upper), AppSettings.Default);
    }

    [Fact]
    public async Task SaveAsync_ThenLoad_RoundTrips()
    {
        var settings = new AppSettings(ThemeMode.Dark, 16, 50_000, KeywordCase.Preserve);
        await NewStore().SaveAsync(settings, _ct);
        await NewStore().SaveAsync(settings, _ct);

        Assert.Equal(settings, await NewStore().LoadAsync(_ct));
    }

    [Theory]
    [InlineData(1, 1, 8, 1_000)]
    [InlineData(100, int.MaxValue, 32, 10_000_000)]
    public async Task LoadAsync_OutOfRangeValues_AreClamped(double font, int rows, double expectedFont, int expectedRows)
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(
            Path.Combine(_dir, "settings.json"),
            $$"""[{"theme":"Light","editorFontSize":{{font}},"maxGridRows":{{rows}},"formatterKeywordCase":"Lower"}]""",
            _ct);

        Assert.Equal(
            new AppSettings(ThemeMode.Light, expectedFont, expectedRows, KeywordCase.Lower),
            await NewStore().LoadAsync(_ct));
    }

    [Fact]
    public async Task LoadAsync_CorruptFile_ReturnsDefaultsAndRenamesIt()
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(Path.Combine(_dir, "settings.json"), "{ nope", _ct);

        Assert.Equal(AppSettings.Default, await NewStore().LoadAsync(_ct));
        Assert.True(File.Exists(Path.Combine(_dir, "settings.json.bad")));
    }
}
