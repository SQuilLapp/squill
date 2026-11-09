using Squill.Core;
using Squill.Platform.Settings;
using Squill.Platform.Updates;
using Xunit;

namespace Squill.Platform.Tests;

public sealed class UpdateStateTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "squill-tests-" + Guid.NewGuid().ToString("N"));
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void AppSettings_Defaults_AreNotAskedAndStable()
    {
        Assert.Equal(UpdateCheckFrequency.NotAsked, AppSettings.Default.UpdateFrequency);
        Assert.Equal(UpdateChannel.Stable, AppSettings.Default.UpdateChannel);
    }

    [Fact]
    public async Task Settings_LegacyFileWithoutUpdateFields_LoadsDefaults()
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(
            Path.Combine(_dir, "settings.json"),
            """[{"theme":"Dark","editorFontSize":14,"maxGridRows":2000,"formatterKeywordCase":"Lower","language":"English"}]""",
            _ct);
        using var store = new JsonSettingsStore(new AppPaths(_dir));

        var loaded = await store.LoadAsync(_ct);

        Assert.Equal(UpdateCheckFrequency.NotAsked, loaded.UpdateFrequency);
        Assert.Equal(UpdateChannel.Stable, loaded.UpdateChannel);
        Assert.Equal(ThemeMode.Dark, loaded.Theme);
    }

    [Fact]
    public async Task Settings_UpdateFields_RoundTrip()
    {
        var settings = AppSettings.Default with { UpdateFrequency = UpdateCheckFrequency.Weekly, UpdateChannel = UpdateChannel.Beta };
        using var store = new JsonSettingsStore(new AppPaths(_dir));
        await store.SaveAsync(settings, _ct);

        Assert.Equal(settings, await store.LoadAsync(_ct));
        Assert.Contains("\"Weekly\"", await File.ReadAllTextAsync(Path.Combine(_dir, "settings.json"), _ct));
    }

    [Theory]
    [InlineData(UpdateCheckFrequency.NotAsked, null, false)]
    [InlineData(UpdateCheckFrequency.NotAsked, -1000, false)]
    [InlineData(UpdateCheckFrequency.Never, null, false)]
    [InlineData(UpdateCheckFrequency.Never, -1000, false)]
    [InlineData(UpdateCheckFrequency.Startup, null, true)]
    [InlineData(UpdateCheckFrequency.Startup, -1, true)]
    [InlineData(UpdateCheckFrequency.Daily, null, true)]
    [InlineData(UpdateCheckFrequency.Daily, -1199, false)] // 19h59
    [InlineData(UpdateCheckFrequency.Daily, -1200, true)] // 20h
    [InlineData(UpdateCheckFrequency.Daily, 60, true)] // future
    [InlineData(UpdateCheckFrequency.Weekly, null, true)]
    [InlineData(UpdateCheckFrequency.Weekly, -9359, false)] // 6.5 days minus a minute
    [InlineData(UpdateCheckFrequency.Weekly, -9360, true)] // 6.5 days
    [InlineData(UpdateCheckFrequency.Weekly, 60, true)] // future
    public void IsDue_Table(UpdateCheckFrequency frequency, int? minutesFromNow, bool expected)
    {
        DateTimeOffset? last = minutesFromNow is { } m ? Now.AddMinutes(m) : null;

        Assert.Equal(expected, UpdateSchedule.IsDue(frequency, last, Now));
    }

    [Fact]
    public async Task Store_RoundTrips_AndWritesOnlyInDataDirectory()
    {
        var state = new UpdateState(Now, "1.2.0", "1.3.0");
        using var store = new JsonUpdateStateStore(new AppPaths(_dir));
        await store.SaveAsync(state, _ct);

        Assert.Equal(state, await store.LoadAsync(_ct));
        Assert.Equal(["update-state.json"], Directory.GetFileSystemEntries(_dir).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Store_MissingFile_GivesEmptyState()
    {
        using var store = new JsonUpdateStateStore(new AppPaths(_dir));

        Assert.Equal(new UpdateState(), await store.LoadAsync(_ct));
    }

    [Fact]
    public async Task Store_CorruptFile_GivesEmptyStateAndRenamesIt()
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(Path.Combine(_dir, "update-state.json"), "{ nope", _ct);
        using var store = new JsonUpdateStateStore(new AppPaths(_dir));

        Assert.Equal(new UpdateState(), await store.LoadAsync(_ct));
        Assert.True(File.Exists(Path.Combine(_dir, "update-state.json.bad")));
    }
}
