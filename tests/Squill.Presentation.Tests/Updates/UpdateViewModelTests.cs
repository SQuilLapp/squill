using Squill.Core;
using Squill.Platform.Settings;
using Squill.Platform.Updates;
using Squill.Presentation.Localization;
using Squill.Presentation.Updates;
using Squill.Tests.Shared;
using Xunit;

namespace Squill.Presentation.Tests.Updates;

public sealed class UpdateViewModelTests : IDisposable
{
    private readonly FakeUpdateEngine _engine = new();
    private readonly FakeSettingsStore _settings = new();
    private readonly FakeStateStore _state = new();
    private readonly UpdateViewModel _vm;

    public UpdateViewModelTests()
    {
        _settings.Current = _settings.Current with { UpdateFrequency = UpdateCheckFrequency.Daily };
        _vm = new UpdateViewModel(
            new UpdateService(_engine, _settings, _state, Ver.Of("1.0.0"), new FakeClock(new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero))),
            new ImmediateDispatcher(),
            _settings);
    }

    public void Dispose() => _vm.Dispose();

    private static UpdateOffer Offer(string version, string? notes = "# Notes\n- one") =>
        new(Ver.Of(version), notes, 13_002_342, IsDelta: true, new object());

    private async Task CheckAsync(UpdateOffer? offer)
    {
        _engine.CheckResult = offer;
        await _vm.CheckNowCommand.ExecuteAsync(null);
    }

    [Fact]
    public async Task Banner_FollowsTheStatus()
    {
        Assert.False(_vm.IsBannerVisible);

        await CheckAsync(null);
        Assert.Equal(UpdateStatus.UpToDate, _vm.Status);
        Assert.False(_vm.IsBannerVisible);

        await CheckAsync(Offer("2.0.0"));
        Assert.True(_vm.IsBannerVisible);
        Assert.Equal("SQuilL 2.0.0 est disponible", _vm.BannerText);

        _engine.ProgressSteps = [40];
        var downloading = new List<string>();
        _vm.PropertyChanged += (_, _) =>
        {
            if (_vm.Status == UpdateStatus.Downloading)
            {
                downloading.Add(_vm.BannerText);
            }
        };
        await _vm.DownloadCommand.ExecuteAsync(null);
        Assert.Contains("Téléchargement de 2.0.0... 40 %", downloading);
        Assert.Equal(UpdateStatus.Ready, _vm.Status);
        Assert.True(_vm.IsBannerVisible);
        Assert.Equal("SQuilL 2.0.0 est prêt : redémarrez pour mettre à jour", _vm.BannerText);
    }

    [Fact]
    public async Task Banner_FailedShowsOnlyWithAnOffer()
    {
        _engine.CheckError = new(UpdateFailure.Network, "boom");
        await CheckAsync(null);
        Assert.Equal(UpdateStatus.Failed, _vm.Status);
        Assert.False(_vm.IsBannerVisible);

        _engine.CheckError = null;
        _engine.DownloadError = new(UpdateFailure.Disk, "disk full");
        await CheckAsync(Offer("2.0.0"));
        await _vm.DownloadCommand.ExecuteAsync(null);
        Assert.Equal(UpdateStatus.Failed, _vm.Status);
        Assert.True(_vm.IsBannerVisible);
        Assert.StartsWith("Échec de la mise à jour", _vm.BannerText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Banner_NotInstalledIsHiddenAndExplained()
    {
        _engine.IsInstalled = false;
        await _vm.CheckNowCommand.ExecuteAsync(null);
        Assert.Equal(UpdateStatus.NotInstalled, _vm.Status);
        Assert.False(_vm.IsBannerVisible);
        Assert.Contains("désactivées", _vm.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Later_HidesTheBannerUntilANewVersion()
    {
        await CheckAsync(Offer("2.0.0"));
        _vm.LaterCommand.Execute(null);
        Assert.False(_vm.IsBannerVisible);
        Assert.Equal(UpdateStatus.Available, _vm.Status);

        await CheckAsync(Offer("3.0.0"));
        Assert.True(_vm.IsBannerVisible);
    }

    [Fact]
    public async Task Start_NotAsked_AsksAndMakesNoEngineCall()
    {
        _settings.Current = _settings.Current with { UpdateFrequency = UpdateCheckFrequency.NotAsked };

        await _vm.StartAsync();

        Assert.True(_vm.ShouldAskFrequency);
        Assert.Equal(0, _engine.CheckCalls);
    }

    [Fact]
    public async Task Start_NotInstalled_DoesNotAsk()
    {
        _engine.IsInstalled = false;
        _settings.Current = _settings.Current with { UpdateFrequency = UpdateCheckFrequency.NotAsked };

        await _vm.StartAsync();

        Assert.False(_vm.ShouldAskFrequency);
        Assert.Equal(0, _engine.CheckCalls);
    }

    [Fact]
    public async Task Start_WhenDue_ChecksOnceAndShowsLastCheck()
    {
        _engine.CheckResult = Offer("2.0.0");

        await _vm.StartAsync();

        Assert.False(_vm.ShouldAskFrequency);
        Assert.Equal(1, _engine.CheckCalls);
        Assert.Equal(UpdateStatus.Available, _vm.Status);
        Assert.StartsWith("Dernière recherche", _vm.LastCheckedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Start_ShowsTheStoredLastCheckWhenNothingIsDue()
    {
        _state.Current = new(LastCheckUtc: new DateTimeOffset(2026, 11, 9, 11, 0, 0, TimeSpan.Zero));

        await _vm.StartAsync();

        Assert.Equal(0, _engine.CheckCalls);
        Assert.StartsWith("Dernière recherche", _vm.LastCheckedText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(UpdateCheckFrequency.Weekly, 1)]
    [InlineData(UpdateCheckFrequency.Never, 0)]
    public async Task ChooseFrequency_SavesAndChecksUnlessNever(UpdateCheckFrequency chosen, int checks)
    {
        _settings.Current = _settings.Current with { UpdateFrequency = UpdateCheckFrequency.NotAsked };
        await _vm.StartAsync();

        await _vm.ChooseFrequencyAsync(chosen);

        Assert.Equal(chosen, _settings.Current.UpdateFrequency);
        Assert.False(_vm.ShouldAskFrequency);
        Assert.Equal(checks, _engine.CheckCalls);
    }

    [Fact]
    public async Task ChooseFrequency_NotAskedChangesNothing()
    {
        _settings.Current = _settings.Current with { UpdateFrequency = UpdateCheckFrequency.NotAsked };

        await _vm.ChooseFrequencyAsync(UpdateCheckFrequency.NotAsked);

        Assert.Equal(UpdateCheckFrequency.NotAsked, _settings.Current.UpdateFrequency);
        Assert.Equal(0, _engine.CheckCalls);
    }

    [Fact]
    public async Task Flow_CheckDownloadRestart_AppliesOnce()
    {
        var offer = Offer("2.0.0");
        await CheckAsync(offer);
        Assert.True(_vm.CanDownload);
        Assert.False(_vm.CanRestart);

        await _vm.DownloadCommand.ExecuteAsync(null);
        Assert.True(_vm.CanRestart);
        Assert.True(_vm.RestartToUpdateCommand.CanExecute(null));
        _vm.RestartToUpdateCommand.Execute(null);

        Assert.Same(offer, _engine.Applied);
        Assert.Equal(1, _engine.DownloadCalls);
        Assert.Equal("2.0.0", _vm.AvailableVersion);
        Assert.True(_vm.IsDelta);
        Assert.True(_vm.HasReleaseNotes);
        Assert.Equal("12,4 Mo", _vm.DownloadSizeText.Replace(' ', ' '));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Restart_AsksFirstAndIsRefusedWhenTheAnswerIsNo(bool agree)
    {
        var offer = Offer("2.0.0");
        await CheckAsync(offer);
        await _vm.DownloadCommand.ExecuteAsync(null);
        var asked = 0;
        _vm.ConfirmRestartAsync = () =>
        {
            asked++;
            return Task.FromResult(agree);
        };

        await _vm.RestartToUpdateCommand.ExecuteAsync(null);

        Assert.Equal(1, asked);
        Assert.Equal(agree ? offer : null, _engine.Applied);
    }

    [Fact]
    public async Task Skip_PersistsAndSilencesTheNextScheduledCheck()
    {
        await CheckAsync(Offer("2.0.0"));
        await _vm.SkipVersionCommand.ExecuteAsync(null);
        Assert.Equal("2.0.0", _state.Current.SkippedVersion);
        Assert.False(_vm.IsBannerVisible);

        _settings.Current = _settings.Current with { UpdateFrequency = UpdateCheckFrequency.Startup };
        await _vm.StartAsync();

        Assert.Equal(2, _engine.CheckCalls);
        Assert.Equal(UpdateStatus.UpToDate, _vm.Status);
        Assert.False(_vm.IsBannerVisible);
    }

    [Fact]
    public async Task Retry_ChecksAgainWithoutAnOfferAndDownloadsWithOne()
    {
        _engine.CheckError = new(UpdateFailure.Network, "down");
        await CheckAsync(null);
        _engine.CheckError = null;
        _engine.CheckResult = Offer("2.0.0");
        await _vm.RetryCommand.ExecuteAsync(null);
        Assert.Equal(UpdateStatus.Available, _vm.Status);

        _engine.DownloadError = new(UpdateFailure.Network, "down");
        await _vm.DownloadCommand.ExecuteAsync(null);
        Assert.Equal(UpdateStatus.Failed, _vm.Status);
        _engine.DownloadError = null;
        await _vm.RetryCommand.ExecuteAsync(null);
        Assert.Equal(UpdateStatus.Ready, _vm.Status);
    }

    [Theory]
    [InlineData(UpdateFailure.Network)]
    [InlineData(UpdateFailure.FeedInvalid)]
    [InlineData(UpdateFailure.Disk)]
    [InlineData(UpdateFailure.VerificationFailed)]
    [InlineData(UpdateFailure.NotInstalled)]
    [InlineData(UpdateFailure.Unknown)]
    public async Task Failure_MapsToALocalizedTextInBothLanguages(UpdateFailure kind)
    {
        _engine.CheckError = new(kind, "raw exception text");
        await CheckAsync(null);

        var texts = new List<string>();
        foreach (var language in new[] { UiLanguage.French, UiLanguage.English })
        {
            using (UiText.Override(language))
            {
                texts.Add(_vm.FailureText);
                Assert.Equal(_vm.FailureText, _vm.StatusText);
            }
        }

        Assert.All(texts, text => Assert.False(string.IsNullOrWhiteSpace(text) || text.StartsWith('!') || text.Contains("raw", StringComparison.Ordinal)));
        Assert.NotEqual(texts[0], texts[1]);
        Assert.Equal("raw exception text", _vm.FailureDetails);
    }

    [Fact]
    public async Task Commands_AreDisabledWhileBusy()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _engine.CheckGate = gate;
        var running = _vm.CheckNowCommand.ExecuteAsync(null);

        Assert.True(_vm.IsBusy);
        Assert.False(_vm.CanCheck);
        Assert.False(_vm.CheckNowCommand.CanExecute(null));
        Assert.False(_vm.DownloadCommand.CanExecute(null));
        Assert.Equal("Recherche en cours...", _vm.StatusText);

        gate.SetResult();
        await running;
        Assert.False(_vm.IsBusy);
        Assert.True(_vm.CheckNowCommand.CanExecute(null));
    }

    [Fact]
    public async Task Dispose_CancelsARunningCheck()
    {
        _engine.CheckGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = _vm.CheckNowCommand.ExecuteAsync(null);

        _vm.Dispose();
        await running;

        Assert.Equal(UpdateStatus.Idle, _vm.Status);
    }

    [Fact]
    public async Task Texts_ExistInBothLanguages()
    {
        await CheckAsync(Offer("2.0.0"));
        foreach (var language in new[] { UiLanguage.French, UiLanguage.English })
        {
            using (UiText.Override(language))
            {
                Assert.All(
                    new[] { _vm.BannerText, _vm.StatusText, _vm.DownloadSizeText, _vm.LastCheckedText },
                    text => Assert.False(string.IsNullOrWhiteSpace(text) || text.Contains('!', StringComparison.Ordinal)));
            }
        }

        using (UiText.Override(UiLanguage.English))
        {
            Assert.Equal("SQuilL 2.0.0 is available", _vm.BannerText);
            Assert.Equal("12.4 MB", _vm.DownloadSizeText);
            Assert.Equal("Update available: 2.0.0", _vm.StatusText);
        }
    }

    [Fact]
    public void Texts_NeverCheckedAndCurrentVersion()
    {
        using (UiText.Override(UiLanguage.English))
        {
            Assert.Equal("Never checked", _vm.LastCheckedText);
        }

        Assert.Equal(AppVersion.Current, _vm.CurrentVersion);
    }
}
