using Squill.Core;
using Squill.Platform.Settings;
using Squill.Platform.Updates;

namespace Squill.Tests.Shared;

internal sealed class FakeUpdateEngine : IUpdateEngine
{
    public bool IsInstalled { get; set; } = true;

    public SemanticVersion? InstalledVersion { get; set; } = Ver.Of("1.0.0");

    public UpdateOffer? CheckResult { get; set; }

    public UpdateEngineException? CheckError { get; set; }

    public UpdateEngineException? DownloadError { get; set; }

    public int[] ProgressSteps { get; set; } = [];

    /// <summary>When set, a check waits for it (or for cancellation).</summary>
    public TaskCompletionSource? CheckGate { get; set; }

    public int CheckCalls { get; private set; }

    public int DownloadCalls { get; private set; }

    public UpdateChannel? LastChannel { get; private set; }

    public UpdateOffer? Applied { get; private set; }

    public async ValueTask<UpdateOffer?> CheckAsync(UpdateChannel channel, CancellationToken ct)
    {
        CheckCalls++;
        LastChannel = channel;
        if (CheckGate is { } gate)
        {
            await gate.Task.WaitAsync(ct);
        }

        return CheckError is null ? CheckResult : throw CheckError;
    }

    public ValueTask DownloadAsync(UpdateOffer offer, IProgress<int> percent, CancellationToken ct)
    {
        DownloadCalls++;
        foreach (var step in ProgressSteps)
        {
            percent.Report(step);
        }

        return DownloadError is null ? ValueTask.CompletedTask : throw DownloadError;
    }

    public void ApplyAndRestart(UpdateOffer offer) => Applied = offer;
}

internal sealed class FakeSettingsStore : ISettingsStore
{
    public AppSettings Current { get; set; } = AppSettings.Default;

    public ValueTask<AppSettings> LoadAsync(CancellationToken ct) => ValueTask.FromResult(Current);

    public ValueTask SaveAsync(AppSettings settings, CancellationToken ct)
    {
        Current = settings;
        return ValueTask.CompletedTask;
    }
}

internal sealed class FakeStateStore : IUpdateStateStore
{
    public UpdateState Current { get; set; } = new();

    public ValueTask<UpdateState> LoadAsync(CancellationToken ct) => ValueTask.FromResult(Current);

    public ValueTask SaveAsync(UpdateState state, CancellationToken ct)
    {
        Current = state;
        return ValueTask.CompletedTask;
    }
}

internal sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

internal static class Ver
{
    public static SemanticVersion Of(string text) => SemanticVersion.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
}
