using Squill.Core;
using Squill.Platform.Settings;

namespace Squill.Platform.Updates;

/// <summary>
/// Runs the update flow (check, download, restart) over an <see cref="IUpdateEngine"/>. It never polls and never starts a
/// thread: the heads call <see cref="CheckIfDueAsync"/> at startup. Every state change raises <see cref="Changed"/> outside the lock.
/// </summary>
public sealed class UpdateService
{
    private readonly IUpdateEngine _engine;
    private readonly ISettingsStore _settings;
    private readonly IUpdateStateStore _state;
    private readonly TimeProvider _time;
    private readonly SemanticVersion _currentVersion;
    private readonly Lock _gate = new();
    private UpdateStatus _status;
    private UpdateOffer? _offer;
    private int _progress;
    private UpdateFailure? _failure;
    private string? _failureMessage;
    private DateTimeOffset? _lastCheckUtc;

    /// <summary>Initializes the service.</summary>
    /// <param name="engine">The packaging-specific engine.</param>
    /// <param name="settings">Source of the channel and the check frequency, read at every call.</param>
    /// <param name="state">Persisted last check and skipped version.</param>
    /// <param name="currentVersion">The running version, used when the engine reports none.</param>
    /// <param name="time">Clock; defaults to <see cref="TimeProvider.System"/>.</param>
    public UpdateService(IUpdateEngine engine, ISettingsStore settings, IUpdateStateStore state, SemanticVersion currentVersion, TimeProvider? time = null)
    {
        _engine = engine;
        _settings = settings;
        _state = state;
        _currentVersion = currentVersion;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Raised after every change of the properties; never while the service holds its lock.</summary>
    public event Action? Changed;

    /// <summary>Gets a value indicating whether the application was installed by the installer, so updates are possible.</summary>
    public bool IsInstalled => _engine.IsInstalled;

    /// <summary>Gets the current status.</summary>
    public UpdateStatus Status => Read(() => _status);

    /// <summary>Gets the offer found by the last check, kept while it can be downloaded or applied.</summary>
    public UpdateOffer? Offer => Read(() => _offer);

    /// <summary>Gets the download progress from 0 to 100.</summary>
    public int Progress => Read(() => _progress);

    /// <summary>Gets the kind of the last failure while <see cref="Status"/> is <see cref="UpdateStatus.Failed"/>.</summary>
    public UpdateFailure? Failure => Read(() => _failure);

    /// <summary>Gets the message of the last failure.</summary>
    public string? FailureMessage => Read(() => _failureMessage);

    /// <summary>Gets when the last check ran, as far as this service knows.</summary>
    public DateTimeOffset? LastCheckUtc => Read(() => _lastCheckUtc);

    /// <summary>Checks for an update because the user asked; a skipped version is offered again and the skip is cleared.</summary>
    /// <param name="ct">Cancellation token; cancelling returns to <see cref="UpdateStatus.Idle"/> without recording a check.</param>
    /// <returns>A task that completes when the check is over; returns at once while another operation runs.</returns>
    public ValueTask CheckNowAsync(CancellationToken ct) => CheckAsync(manual: true, ct);

    /// <summary>Checks only when the user's frequency says a check is due, the application is installed and nothing is running.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the check is over or was not needed.</returns>
    public async ValueTask CheckIfDueAsync(CancellationToken ct)
    {
        if (!_engine.IsInstalled || Read(() => _status is UpdateStatus.Checking or UpdateStatus.Downloading))
        {
            return;
        }

        var frequency = (await _settings.LoadAsync(ct)).UpdateFrequency;
        if (frequency is UpdateCheckFrequency.NotAsked or UpdateCheckFrequency.Never)
        {
            return;
        }

        var state = await _state.LoadAsync(ct);
        if (state.LastCheckUtc is { } stored)
        {
            Apply(() => _lastCheckUtc ??= stored);
        }

        if (UpdateSchedule.IsDue(frequency, state.LastCheckUtc, _time.GetUtcNow()))
        {
            await CheckAsync(manual: false, ct);
        }
    }

    /// <summary>Downloads the offer, from <see cref="UpdateStatus.Available"/> or to retry after a failed download.</summary>
    /// <param name="ct">Cancellation token; cancelling goes back to <see cref="UpdateStatus.Available"/>.</param>
    /// <returns>A task that completes when the download is over; does nothing in any other state.</returns>
    public async ValueTask DownloadAsync(CancellationToken ct)
    {
        UpdateOffer offer;
        lock (_gate)
        {
            if (_offer is null || _status is not (UpdateStatus.Available or UpdateStatus.Failed))
            {
                return;
            }

            offer = _offer;
            _status = UpdateStatus.Downloading;
            _progress = 0;
            _failure = null;
            _failureMessage = null;
        }

        Changed?.Invoke();
        try
        {
            await _engine.DownloadAsync(offer, new Reporter(SetProgress), ct);
            Apply(() =>
            {
                _status = UpdateStatus.Ready;
                _progress = 100;
            });
        }
        catch (OperationCanceledException)
        {
            Apply(() =>
            {
                _status = UpdateStatus.Available;
                _progress = 0;
            });
        }
        catch (UpdateEngineException ex)
        {
            Fail(ex, keepOffer: true);
        }
    }

    /// <summary>Stops notifying about the offered version until the user checks manually.</summary>
    /// <returns>A task that completes when the choice is saved; does nothing unless an update is available.</returns>
    public async ValueTask SkipVersionAsync()
    {
        var offer = Read(() => _status == UpdateStatus.Available ? _offer : null);
        if (offer is null)
        {
            return;
        }

        var state = await _state.LoadAsync(CancellationToken.None);
        await _state.SaveAsync(state with { SkippedVersion = offer.Version.ToString() }, CancellationToken.None);
        Apply(() =>
        {
            _status = UpdateStatus.UpToDate;
            _offer = null;
        });
    }

    /// <summary>Applies the downloaded update and restarts; does not return when it succeeds.</summary>
    /// <exception cref="InvalidOperationException">No update is ready.</exception>
    public void RestartToUpdate()
    {
        var offer = Read(() => _status == UpdateStatus.Ready ? _offer : null)
            ?? throw new InvalidOperationException("No update is ready to install.");
        _engine.ApplyAndRestart(offer);
    }

    private async ValueTask CheckAsync(bool manual, CancellationToken ct)
    {
        if (!_engine.IsInstalled)
        {
            Apply(() => _status = UpdateStatus.NotInstalled);
            return;
        }

        lock (_gate)
        {
            if (_status is UpdateStatus.Checking or UpdateStatus.Downloading)
            {
                return;
            }

            _status = UpdateStatus.Checking;
            _offer = null;
            _failure = null;
            _failureMessage = null;
        }

        Changed?.Invoke();
        try
        {
            var channel = (await _settings.LoadAsync(ct)).UpdateChannel;
            var state = await _state.LoadAsync(ct);
            UpdateOffer? found = null;
            UpdateEngineException? error = null;
            try
            {
                found = await _engine.CheckAsync(channel, ct);
            }
            catch (UpdateEngineException ex)
            {
                error = ex;
            }

            var now = _time.GetUtcNow();
            var installed = _engine.InstalledVersion ?? _currentVersion;
            var offer = found is not null && found.Version > installed ? found : null;
            var skipped = offer is not null && offer.Version.ToString() == state.SkippedVersion;
            await _state.SaveAsync(
                state with
                {
                    LastCheckUtc = now,
                    LastSeenVersion = offer?.Version.ToString() ?? state.LastSeenVersion,
                    SkippedVersion = manual && skipped ? null : state.SkippedVersion,
                },
                ct);

            if (error is not null)
            {
                Fail(error, keepOffer: false, now);
                return;
            }

            var show = offer is not null && (manual || !skipped);
            Apply(() =>
            {
                _lastCheckUtc = now;
                _offer = show ? offer : null;
                _status = show ? UpdateStatus.Available : UpdateStatus.UpToDate;
            });
        }
        catch (OperationCanceledException)
        {
            Apply(() => _status = UpdateStatus.Idle);
        }
    }

    private void Fail(UpdateEngineException ex, bool keepOffer, DateTimeOffset? checkedAt = null) => Apply(() =>
    {
        _status = UpdateStatus.Failed;
        _failure = ex.Kind;
        _failureMessage = ex.Message;
        _progress = 0;
        _lastCheckUtc = checkedAt ?? _lastCheckUtc;
        if (!keepOffer)
        {
            _offer = null;
        }
    });

    private void SetProgress(int percent) => Apply(() => _progress = Math.Clamp(percent, 0, 100));

    private T Read<T>(Func<T> read)
    {
        lock (_gate)
        {
            return read();
        }
    }

    private void Apply(Action mutate)
    {
        lock (_gate)
        {
            mutate();
        }

        Changed?.Invoke();
    }

    private sealed class Reporter(Action<int> report) : IProgress<int>
    {
        public void Report(int value) => report(value);
    }
}
