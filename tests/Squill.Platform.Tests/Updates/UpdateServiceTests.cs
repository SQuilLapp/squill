using Squill.Core;
using Squill.Platform.Settings;
using Squill.Platform.Updates;
using Squill.Tests.Shared;
using Xunit;

namespace Squill.Platform.Tests.Updates;

public sealed class UpdateServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly SemanticVersion V1 = Ver.Of("1.0.0");
    private static readonly SemanticVersion V2 = Ver.Of("2.0.0");
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly FakeUpdateEngine _engine = new();
    private readonly FakeSettingsStore _settings = new();
    private readonly FakeStateStore _state = new();
    private readonly FakeClock _clock = new(Now);

    private UpdateService NewService() => new(_engine, _settings, _state, V1, _clock);

    private static UpdateOffer Offer(SemanticVersion version) => new(version, "notes", 1234, IsDelta: false, new object());

    private void Frequency(UpdateCheckFrequency frequency) =>
        _settings.Current = _settings.Current with { UpdateFrequency = frequency };

    [Theory]
    [InlineData(UpdateCheckFrequency.NotAsked)]
    [InlineData(UpdateCheckFrequency.Never)]
    public async Task CheckIfDue_NotAskedOrNever_MakesNoEngineCall(UpdateCheckFrequency frequency)
    {
        Frequency(frequency);
        var service = NewService();

        await service.CheckIfDueAsync(_ct);

        Assert.Equal(0, _engine.CheckCalls);
        Assert.Equal(UpdateStatus.Idle, service.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NotInstalled_MakesNoEngineCall(bool manual)
    {
        _engine.IsInstalled = false;
        Frequency(UpdateCheckFrequency.Startup);
        var service = NewService();

        if (manual)
        {
            await service.CheckNowAsync(_ct);
            Assert.Equal(UpdateStatus.NotInstalled, service.Status);
        }
        else
        {
            await service.CheckIfDueAsync(_ct);
            Assert.Equal(UpdateStatus.Idle, service.Status);
        }

        Assert.Equal(0, _engine.CheckCalls);
    }

    [Fact]
    public async Task CheckIfDue_Startup_ChecksEveryTime()
    {
        Frequency(UpdateCheckFrequency.Startup);
        var service = NewService();

        await service.CheckIfDueAsync(_ct);
        await service.CheckIfDueAsync(_ct);

        Assert.Equal(2, _engine.CheckCalls);
        Assert.Equal(UpdateStatus.UpToDate, service.Status);
    }

    [Theory]
    [InlineData(UpdateCheckFrequency.Daily, 19, 0)]
    [InlineData(UpdateCheckFrequency.Daily, 20, 1)]
    [InlineData(UpdateCheckFrequency.Weekly, 24 * 6, 0)]
    [InlineData(UpdateCheckFrequency.Weekly, 24 * 7, 1)]
    public async Task CheckIfDue_DailyAndWeekly_FollowTheClock(UpdateCheckFrequency frequency, int hoursSince, int expectedCalls)
    {
        Frequency(frequency);
        _state.Current = new UpdateState(Now.AddHours(-hoursSince));
        var service = NewService();

        await service.CheckIfDueAsync(_ct);

        Assert.Equal(expectedCalls, _engine.CheckCalls);
    }

    [Fact]
    public async Task Check_Success_RecordsLastCheckAndSeenVersion()
    {
        _engine.CheckResult = Offer(V2);
        var service = NewService();

        await service.CheckNowAsync(_ct);

        Assert.Equal(UpdateStatus.Available, service.Status);
        Assert.Equal(V2, service.Offer!.Version);
        Assert.Equal(Now, service.LastCheckUtc);
        Assert.Equal(new UpdateState(Now, null, "2.0.0"), _state.Current);
    }

    [Fact]
    public async Task Check_Failure_StillRecordsLastCheck()
    {
        _engine.CheckError = new UpdateEngineException(UpdateFailure.Network, "offline");
        var service = NewService();

        await service.CheckNowAsync(_ct);

        Assert.Equal(UpdateStatus.Failed, service.Status);
        Assert.Equal(UpdateFailure.Network, service.Failure);
        Assert.Equal("offline", service.FailureMessage);
        Assert.Equal(Now, _state.Current.LastCheckUtc);
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("0.9.0")]
    public async Task Check_OfferNotNewerThanInstalled_IsIgnored(string offered)
    {
        _engine.CheckResult = Offer(Ver.Of(offered));
        var service = NewService();

        await service.CheckNowAsync(_ct);

        Assert.Equal(UpdateStatus.UpToDate, service.Status);
        Assert.Null(service.Offer);
    }

    [Fact]
    public async Task Check_UsesCurrentVersionWhenEngineReportsNone()
    {
        _engine.InstalledVersion = null;
        _engine.CheckResult = Offer(V1);
        var service = NewService();

        await service.CheckNowAsync(_ct);

        Assert.Equal(UpdateStatus.UpToDate, service.Status);
    }

    [Fact]
    public async Task SkippedVersion_IsSilentWhenDue_ButShownAndClearedByCheckNow()
    {
        Frequency(UpdateCheckFrequency.Startup);
        _engine.CheckResult = Offer(V2);
        var service = NewService();
        await service.CheckNowAsync(_ct);
        await service.SkipVersionAsync();

        Assert.Equal(UpdateStatus.UpToDate, service.Status);
        Assert.Equal("2.0.0", _state.Current.SkippedVersion);

        await service.CheckIfDueAsync(_ct);
        Assert.Equal(UpdateStatus.UpToDate, service.Status);
        Assert.Equal("2.0.0", _state.Current.SkippedVersion);

        await service.CheckNowAsync(_ct);
        Assert.Equal(UpdateStatus.Available, service.Status);
        Assert.Null(_state.Current.SkippedVersion);
    }

    [Fact]
    public async Task SkipVersion_WithoutOffer_DoesNothing()
    {
        var service = NewService();

        await service.SkipVersionAsync();

        Assert.Null(_state.Current.SkippedVersion);
        Assert.Equal(UpdateStatus.Idle, service.Status);
    }

    [Fact]
    public async Task Cancel_GoesIdleWithoutRecordingACheck()
    {
        _engine.CheckGate = new TaskCompletionSource();
        var service = NewService();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_ct);

        var running = service.CheckNowAsync(cts.Token).AsTask();
        await cts.CancelAsync();
        await running;

        Assert.Equal(UpdateStatus.Idle, service.Status);
        Assert.Null(_state.Current.LastCheckUtc);
    }

    [Fact]
    public async Task Reentrancy_SecondCallWhileCheckingReturnsAtOnce()
    {
        _engine.CheckGate = new TaskCompletionSource();
        var service = NewService();

        var first = service.CheckNowAsync(_ct).AsTask();
        await service.CheckNowAsync(_ct);
        Assert.Equal(UpdateStatus.Checking, service.Status);
        Assert.Equal(1, _engine.CheckCalls);

        _engine.CheckGate.SetResult();
        await first;
        Assert.Equal(UpdateStatus.UpToDate, service.Status);
    }

    [Fact]
    public async Task Download_ReportsProgressAndEndsReady()
    {
        _engine.CheckResult = Offer(V2);
        _engine.ProgressSteps = [10, 50, 100];
        var service = NewService();
        await service.CheckNowAsync(_ct);
        var seen = new List<(UpdateStatus Status, int Progress)>();
        service.Changed += () => seen.Add((service.Status, service.Progress));

        await service.DownloadAsync(_ct);

        Assert.Equal(UpdateStatus.Ready, service.Status);
        Assert.Equal(
            [(UpdateStatus.Downloading, 0), (UpdateStatus.Downloading, 10), (UpdateStatus.Downloading, 50), (UpdateStatus.Downloading, 100), (UpdateStatus.Ready, 100)],
            seen);
    }

    [Fact]
    public async Task Download_Failure_KeepsOfferAndRetryWorks()
    {
        _engine.CheckResult = Offer(V2);
        _engine.DownloadError = new UpdateEngineException(UpdateFailure.Disk, "disk full");
        var service = NewService();
        await service.CheckNowAsync(_ct);

        await service.DownloadAsync(_ct);
        Assert.Equal(UpdateStatus.Failed, service.Status);
        Assert.Equal(UpdateFailure.Disk, service.Failure);
        Assert.NotNull(service.Offer);

        _engine.DownloadError = null;
        await service.DownloadAsync(_ct);
        Assert.Equal(UpdateStatus.Ready, service.Status);
        Assert.Null(service.Failure);
        Assert.Equal(2, _engine.DownloadCalls);
    }

    [Fact]
    public async Task Download_WithoutOffer_DoesNothing()
    {
        var service = NewService();

        await service.DownloadAsync(_ct);

        Assert.Equal(0, _engine.DownloadCalls);
        Assert.Equal(UpdateStatus.Idle, service.Status);
    }

    [Fact]
    public async Task RestartToUpdate_OnlyWhenReady()
    {
        _engine.CheckResult = Offer(V2);
        var service = NewService();
        Assert.Throws<InvalidOperationException>(service.RestartToUpdate);
        await service.CheckNowAsync(_ct);
        Assert.Throws<InvalidOperationException>(service.RestartToUpdate);
        await service.DownloadAsync(_ct);

        service.RestartToUpdate();

        Assert.Same(service.Offer, _engine.Applied);
    }

    [Fact]
    public async Task Check_ReadsChannelFromSettingsEachTime()
    {
        var service = NewService();

        await service.CheckNowAsync(_ct);
        Assert.Equal(UpdateChannel.Stable, _engine.LastChannel);

        _settings.Current = _settings.Current with { UpdateChannel = UpdateChannel.Beta };
        await service.CheckNowAsync(_ct);
        Assert.Equal(UpdateChannel.Beta, _engine.LastChannel);
    }

    [Fact]
    public async Task Changed_FiresOncePerTransition_AndNeverUnderTheLock()
    {
        _engine.CheckResult = Offer(V2);
        var service = NewService();
        var statuses = new List<UpdateStatus>();
        service.Changed += () =>
        {
            // A reader on another thread would block forever if the lock were held here.
            Assert.True(Task.Run(() => service.Status).Wait(TimeSpan.FromSeconds(5)));
            statuses.Add(service.Status);
        };

        await service.CheckNowAsync(_ct);

        Assert.Equal([UpdateStatus.Checking, UpdateStatus.Available], statuses);
    }
}
