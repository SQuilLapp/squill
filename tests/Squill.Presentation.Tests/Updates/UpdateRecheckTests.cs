using Squill.Core;
using Squill.Platform.Settings;
using Squill.Platform.Updates;
using Squill.Presentation.Updates;
using Squill.Tests.Shared;
using Xunit;

namespace Squill.Presentation.Tests.Updates;

public sealed class UpdateRecheckTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

    private static async Task<int> ChecksAfterOneHourAsync(UpdateCheckFrequency frequency)
    {
        var engine = new FakeUpdateEngine();
        var settings = new FakeSettingsStore();
        settings.Current = settings.Current with { UpdateFrequency = frequency };
        var clock = new FakeClock(Start);
        using var vm = new UpdateViewModel(new UpdateService(engine, settings, new FakeStateStore(), Ver.Of("1.0.0"), clock), new ImmediateDispatcher(), settings)
        {
            RecheckInterval = TimeSpan.FromMilliseconds(30),
        };

        await vm.StartAsync();
        var atStart = engine.CheckCalls;
        clock.Now = Start.AddMinutes(61);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        Assert.Equal(frequency == UpdateCheckFrequency.Never ? 0 : 1, atStart);
        return engine.CheckCalls - atStart;
    }

    [Fact]
    public async Task Hourly_ChecksAtStartThenAgainWhenAnHourPassed() => Assert.Equal(1, await ChecksAfterOneHourAsync(UpdateCheckFrequency.Hourly));

    [Theory]
    [InlineData(UpdateCheckFrequency.Startup)]
    [InlineData(UpdateCheckFrequency.Never)]
    public async Task StartupAndNever_NeverRecheckWhileOpen(UpdateCheckFrequency frequency) => Assert.Equal(0, await ChecksAfterOneHourAsync(frequency));
}
