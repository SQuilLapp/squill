using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Squill.Core;
using Squill.Platform.Settings;
using Squill.Platform.Updates;
using Squill.Presentation.Localization;

namespace Squill.Presentation.Updates;

/// <summary>Everything the update banner, the update dialog and the About view show and do, over an <see cref="UpdateService"/>.</summary>
public sealed partial class UpdateViewModel : ViewModelBase, IDisposable
{
    private readonly UpdateService _service;
    private readonly IUiDispatcher _dispatcher;
    private readonly ISettingsStore _settings;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;
    private string? _laterVersion;
    private IReadOnlyList<NotesBlock> _notes = [];
    private string? _notesSource;

    /// <summary>Initializes the view-model and follows the service; it never starts a timer.</summary>
    /// <param name="service">The update flow.</param>
    /// <param name="dispatcher">Marshals service notifications onto the UI thread.</param>
    /// <param name="settings">Where the user's check frequency is read and saved.</param>
    public UpdateViewModel(UpdateService service, IUiDispatcher dispatcher, ISettingsStore settings)
    {
        _service = service;
        _dispatcher = dispatcher;
        _settings = settings;
        _service.Changed += OnServiceChanged;
    }

    /// <summary>Gets the current status of the update flow.</summary>
    public UpdateStatus Status => _service.Status;

    /// <summary>Gets a value indicating whether the one-time question about the check frequency must be shown.</summary>
    [ObservableProperty]
    public partial bool ShouldAskFrequency { get; private set; }

    /// <summary>Gets a value indicating whether the update notice is shown.</summary>
    public bool IsBannerVisible => _service.Offer is { } offer
        && _laterVersion != offer.Version.ToString()
        && (Status is UpdateStatus.Available or UpdateStatus.Downloading or UpdateStatus.Ready or UpdateStatus.Failed);

    /// <summary>Gets the text of the update notice.</summary>
    public string BannerText => (_service.Offer, Status) switch
    {
        (null, _) => string.Empty,
        (_, UpdateStatus.Available) => UiText.Format("update.banner.available", AvailableVersion),
        (_, UpdateStatus.Downloading) => UiText.Format("update.banner.downloading", AvailableVersion, Progress),
        (_, UpdateStatus.Ready) => UiText.Format("update.banner.ready", AvailableVersion),
        (_, UpdateStatus.Failed) => UiText.Format("update.banner.failed", FailureText),
        _ => string.Empty,
    };

    /// <summary>Gets the download progress from 0 to 100.</summary>
    public int Progress => _service.Progress;

    /// <summary>Gets the offered version, or an empty string when there is no offer.</summary>
    public string AvailableVersion => _service.Offer?.Version.ToString() ?? string.Empty;

    /// <summary>Gets the running version.</summary>
    public string CurrentVersion { get; } = AppVersion.Current;

    /// <summary>Gets the release notes of the offered version.</summary>
    public IReadOnlyList<NotesBlock> ReleaseNotes
    {
        get
        {
            var source = _service.Offer?.ReleaseNotesMarkdown;
            if (!ReferenceEquals(source, _notesSource))
            {
                _notesSource = source;
                _notes = Updates.ReleaseNotes.Parse(source);
            }

            return _notes;
        }
    }

    /// <summary>Gets a value indicating whether the offered version has release notes.</summary>
    public bool HasReleaseNotes => ReleaseNotes.Count > 0;

    /// <summary>Gets the download size in the active language, or an empty string when unknown.</summary>
    public string DownloadSizeText => _service.Offer is { DownloadBytes: > 0 } offer
        ? UiText.Format("update.size", offer.DownloadBytes / 1_048_576.0)
        : string.Empty;

    /// <summary>Gets a value indicating whether only a delta package is downloaded.</summary>
    public bool IsDelta => _service.Offer?.IsDelta ?? false;

    /// <summary>Gets the one-line status shown in the About dialog.</summary>
    public string StatusText => Status switch
    {
        UpdateStatus.NotInstalled => UiText.Get("update.status.notinstalled"),
        UpdateStatus.Checking => UiText.Get("update.status.checking"),
        UpdateStatus.UpToDate => UiText.Get("update.status.upToDate"),
        UpdateStatus.Available or UpdateStatus.Downloading or UpdateStatus.Ready => UiText.Format("update.status.available", AvailableVersion),
        UpdateStatus.Failed => FailureText,
        _ => string.Empty,
    };

    /// <summary>Gets the localized reason of the last failure, never the raw exception text.</summary>
    public string FailureText => UiText.Get(_service.Failure switch
    {
        UpdateFailure.Network => "update.failure.network",
        UpdateFailure.FeedInvalid => "update.failure.feed",
        UpdateFailure.Disk => "update.failure.disk",
        UpdateFailure.VerificationFailed => "update.failure.verification",
        UpdateFailure.NotInstalled => "update.failure.notinstalled",
        _ => "update.failure.unknown",
    });

    /// <summary>Gets the raw message of the last failure, for the details line of the dialog only.</summary>
    public string? FailureDetails => _service.FailureMessage;

    /// <summary>Gets when the last check ran, in the active language.</summary>
    public string LastCheckedText => _service.LastCheckUtc is { } at
        ? UiText.Format("update.lastChecked", at.ToLocalTime().ToString("g", UiText.Culture))
        : UiText.Get("update.lastChecked.never");

    /// <summary>Gets a value indicating whether a check or a download is running.</summary>
    public bool IsBusy => Status is UpdateStatus.Checking or UpdateStatus.Downloading;

    /// <summary>Gets a value indicating whether the application was installed by the installer, so updates are possible.</summary>
    public bool IsInstalled => _service.IsInstalled;

    /// <summary>Gets a value indicating whether a check can be started.</summary>
    public bool CanCheck => _service.IsInstalled && !IsBusy;

    /// <summary>Gets a value indicating whether the offer can be downloaded.</summary>
    public bool CanDownload => Status == UpdateStatus.Available;

    /// <summary>Gets a value indicating whether the downloaded update can be applied.</summary>
    public bool CanRestart => Status == UpdateStatus.Ready;

    /// <summary>Gets a value indicating whether the offered version can be skipped.</summary>
    public bool CanSkip => Status == UpdateStatus.Available;

    /// <summary>Gets a value indicating whether the last operation can be retried.</summary>
    public bool CanRetry => Status == UpdateStatus.Failed;

    /// <summary>Checks for an update at startup when the user's frequency says so; asks for the frequency first when the user never chose. Never throws.</summary>
    /// <returns>A task that completes when the startup check is over or was not needed.</returns>
    public async Task StartAsync()
    {
        try
        {
            var settings = await _settings.LoadAsync(_cts.Token);
            ShouldAskFrequency = _service.IsInstalled && settings.UpdateFrequency == UpdateCheckFrequency.NotAsked;
            if (!ShouldAskFrequency)
            {
                await _service.CheckIfDueAsync(_cts.Token);
            }
        }
        catch (Exception) when (!_cts.IsCancellationRequested)
        {
            // A broken settings file must not break the start: the user can still check by hand.
        }
        catch (OperationCanceledException)
        {
            // Disposed while starting.
        }
    }

    /// <summary>Saves the user's answer to the one-time question and runs a first check unless the answer is <see cref="UpdateCheckFrequency.Never"/>.</summary>
    /// <param name="frequency">The chosen frequency; <see cref="UpdateCheckFrequency.NotAsked"/> is ignored.</param>
    /// <returns>A task that completes when the answer is saved and the first check is over.</returns>
    public async Task ChooseFrequencyAsync(UpdateCheckFrequency frequency)
    {
        if (frequency == UpdateCheckFrequency.NotAsked)
        {
            return;
        }

        var settings = await _settings.LoadAsync(_cts.Token);
        await _settings.SaveAsync(settings with { UpdateFrequency = frequency }, _cts.Token);
        ShouldAskFrequency = false;
        if (frequency != UpdateCheckFrequency.Never)
        {
            await _service.CheckNowAsync(_cts.Token);
        }
    }

    /// <summary>Stops listening to the service and cancels a running check or download.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _service.Changed -= OnServiceChanged;
        _cts.Cancel();
        _cts.Dispose();
    }

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private async Task CheckNowAsync()
    {
        _laterVersion = null;
        await _service.CheckNowAsync(_cts.Token);
    }

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadAsync() => await _service.DownloadAsync(_cts.Token);

    [RelayCommand(CanExecute = nameof(CanRestart))]
    private void RestartToUpdate() => _service.RestartToUpdate();

    [RelayCommand(CanExecute = nameof(CanSkip))]
    private async Task SkipVersionAsync() => await _service.SkipVersionAsync();

    [RelayCommand(CanExecute = nameof(IsBannerVisible))]
    private void Later()
    {
        _laterVersion = _service.Offer?.Version.ToString();
        Refresh();
    }

    [RelayCommand(CanExecute = nameof(CanRetry))]
    private async Task RetryAsync()
    {
        if (_service.Offer is null)
        {
            await _service.CheckNowAsync(_cts.Token);
        }
        else
        {
            await _service.DownloadAsync(_cts.Token);
        }
    }

    private void OnServiceChanged()
    {
        if (_dispatcher.CheckAccess())
        {
            Refresh();
        }
        else
        {
            _dispatcher.Post(Refresh);
        }
    }

    private void Refresh()
    {
        OnPropertyChanged(string.Empty);
        CheckNowCommand.NotifyCanExecuteChanged();
        DownloadCommand.NotifyCanExecuteChanged();
        RestartToUpdateCommand.NotifyCanExecuteChanged();
        SkipVersionCommand.NotifyCanExecuteChanged();
        LaterCommand.NotifyCanExecuteChanged();
        RetryCommand.NotifyCanExecuteChanged();
    }
}
