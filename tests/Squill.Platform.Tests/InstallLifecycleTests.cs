using System.Text.Json;
using Squill.Platform.Lifecycle;
using Xunit;

namespace Squill.Platform.Tests;

public sealed class InstallLifecycleTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "squill-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Theory]
    [InlineData(null, "0.1.0", RunKind.FirstRun)]
    [InlineData("not a version", "0.1.0", RunKind.FirstRun)]
    [InlineData("0.1.0", "0.1.0", RunKind.Same)]
    [InlineData("0.1.0", "0.2.0", RunKind.Updated)]
    [InlineData("0.2.0-beta.1", "0.2.0", RunKind.Updated)]
    [InlineData("0.2.0", "0.2.0-beta.1", RunKind.Downgraded)]
    [InlineData("1.0.0", "0.9.9", RunKind.Downgraded)]
    public void Classify_ComparesLastRunWithCurrent(string? last, string current, RunKind expected) =>
        Assert.Equal(expected, InstallLifecycle.Classify(last, current));

    [Fact]
    public void Classify_InvalidCurrentVersion_Throws() =>
        Assert.Throws<FormatException>(() => InstallLifecycle.Classify("0.1.0", "nope"));

    [Fact]
    public void Record_TracksFirstRunThenSameThenUpdated()
    {
        var store = new InstallStateStore(new AppPaths(_dir));

        Assert.Equal(RunKind.FirstRun, store.Record("0.1.0"));
        Assert.Equal(RunKind.Same, store.Record("0.1.0"));
        Assert.Equal(RunKind.Updated, store.Record("0.2.0"));
        Assert.Equal("0.2.0", store.Load()?.LastRunVersion);
    }

    [Fact]
    public void Save_WritesOnlyTheVersionInTheDataDirectory()
    {
        new InstallStateStore(new AppPaths(_dir)).Save(new InstallState("0.3.0-beta.2"));

        var files = Directory.GetFileSystemEntries(_dir, "*", SearchOption.AllDirectories);
        Assert.Equal([Path.Combine(_dir, InstallLifecycle.StateFileName)], files);
        using var json = JsonDocument.Parse(File.ReadAllText(files[0]));
        var property = Assert.Single(json.RootElement.EnumerateObject());
        Assert.Equal("lastRunVersion", property.Name);
        Assert.Equal("0.3.0-beta.2", property.Value.GetString());
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("null")]
    public void Record_CorruptFile_IsFirstRunAndGetsOverwritten(string content)
    {
        var paths = new AppPaths(_dir);
        File.WriteAllText(paths.Combine(InstallLifecycle.StateFileName), content);
        var store = new InstallStateStore(paths);

        Assert.Equal(RunKind.FirstRun, store.Record("0.1.0"));
        Assert.Equal("0.1.0", store.Load()?.LastRunVersion);
    }
}
