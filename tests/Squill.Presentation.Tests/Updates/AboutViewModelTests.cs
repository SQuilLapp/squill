using Squill.Platform.Lifecycle;
using Squill.Platform.Updates;
using Squill.Presentation.Localization;
using Squill.Presentation.Updates;
using Squill.Tests.Shared;
using Xunit;

namespace Squill.Presentation.Tests.Updates;

public sealed class AboutViewModelTests
{
    private const string Changelog = "# Changelog\n\n## [Unreleased]\n\n_Nothing yet._\n\n## [1.2.0] - 2026-01-01\n\n### Fixed\n\n- A bug\n\n## [1.1.0] - 2025-12-01\n\n- Old\n";

    private static AboutViewModel Create(
        bool installed = true,
        RunKind kind = RunKind.Same,
        string version = "1.2.0",
        string? changelog = Changelog)
    {
        var engine = new FakeUpdateEngine { IsInstalled = installed };
        var settings = new FakeSettingsStore();
        var service = new UpdateService(engine, settings, new FakeStateStore(), Ver.Of(version));
        var updates = new UpdateViewModel(service, new ImmediateDispatcher(), settings);
        return new AboutViewModel(updates, @"C:\data", kind, "1.1.0", version, changelog);
    }

    [Fact]
    public void Texts_InstalledStable_English()
    {
        using var scope = UiText.Override(UiLanguage.English);

        var about = Create();

        Assert.Equal("SQuilL", about.ProductName);
        Assert.Equal("Version 1.2.0", about.VersionText);
        Assert.Equal("Channel: Stable", about.ChannelText);
        Assert.Equal("Installed with the installer", about.InstallKindText);
        Assert.StartsWith(".NET ", about.RuntimeText, StringComparison.Ordinal);
        Assert.Equal("https://github.com/squillapp/squill", about.RepositoryUrl);
        Assert.Equal("License: MIT", about.LicenseText);
        Assert.Equal(@"C:\data", about.DataFolder);
    }

    [Fact]
    public void Texts_PortablePreRelease_French()
    {
        using var scope = UiText.Override(UiLanguage.French);

        var about = Create(installed: false, version: "1.3.0-beta.1");

        Assert.Equal("Canal : Beta", about.ChannelText);
        Assert.Equal("Non installé (mises à jour désactivées)", about.InstallKindText);
    }

    [Theory]
    [InlineData(RunKind.Updated, true)]
    [InlineData(RunKind.Same, false)]
    [InlineData(RunKind.FirstRun, false)]
    public void WhatsNew_OnlyAfterAnUpdate(RunKind kind, bool shown)
    {
        using var scope = UiText.Override(UiLanguage.English);

        var about = Create(kind: kind);

        Assert.Equal(shown, about.ShowWhatsNew);
        Assert.Equal("Updated from 1.1.0", about.WhatsNewText);
    }

    [Fact]
    public void Notes_AreTheSectionOfTheRunningVersion()
    {
        var notes = Create().Notes;

        Assert.Equal(2, notes.Count);
        Assert.Equal("Fixed", Assert.IsType<NotesHeading>(notes[0]).Text);
        Assert.IsType<NotesBullet>(notes[1]);
    }

    [Fact]
    public void Notes_WithoutChangelog_AreEmpty() => Assert.Empty(Create(changelog: null).Notes);

    [Fact]
    public void OpenDataFolder_RaisesTheRequest()
    {
        var about = Create();
        object? sender = null;
        about.OpenDataFolderRequested += (s, _) => sender = s;

        about.OpenDataFolderCommand.Execute(null);

        Assert.Same(about, sender);
    }

    [Fact]
    public void EmbeddedChangelog_IsFoundAndNotEmpty()
    {
        var text = AboutViewModel.ReadEmbeddedChangelog();

        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.Contains("## [Unreleased]", text, StringComparison.Ordinal);
        Assert.NotEmpty(Create(changelog: text, version: "0.1.0").Notes);
    }

    [Theory]
    [InlineData("1.2.0", "### Fixed\n\n- A bug")]
    [InlineData("1.1.0", "- Old")]
    [InlineData("9.9.9", "")]
    [InlineData("Unreleased", "")]
    public void Extract_CutsTheSectionAndFallsBackToUnreleased(string version, string expected) =>
        Assert.Equal(expected, ChangelogSection.Extract(Changelog, version));

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    [InlineData("no sections here")]
    public void Extract_WithoutSections_IsEmpty(string? changelog) =>
        Assert.Equal(string.Empty, ChangelogSection.Extract(changelog, "1.0.0"));
}
