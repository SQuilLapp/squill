using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Squill.App.Avalonia.Views;
using Squill.Platform.Updates;
using Squill.Presentation.Localization;
using Squill.Presentation.Updates;

namespace Squill.App.Avalonia;

// The update notice above the workspace, the update window, the About window and the start-up trigger.
public sealed partial class MainWindow
{
    private UpdateViewModel? _updates;
    private AboutViewModel? _about;
    private UpdateWindow? _updateWindow;

    private void AttachUpdates(UpdateViewModel updates, AboutViewModel about)
    {
        _updates = updates;
        _about = about;
        updates.PropertyChanged += OnUpdatesChanged;
        RefreshBanner();
    }

    private void OnUpdatesChanged(object? sender, PropertyChangedEventArgs e) => RefreshBanner();

    private void RefreshBanner()
    {
        if (_updates is not { } updates)
            return;
        var parts = UpdateBannerLayout.PartsFor(updates.Status);
        BannerDetails.IsVisible = parts.HasFlag(BannerParts.Details);
        BannerDownload.IsVisible = parts.HasFlag(BannerParts.Download);
        BannerProgress.IsVisible = parts.HasFlag(BannerParts.Progress);
        BannerRestart.IsVisible = parts.HasFlag(BannerParts.Restart);
        BannerRetry.IsVisible = parts.HasFlag(BannerParts.Retry);
        BannerProgress.Value = updates.Progress;
        BannerText.Text = updates.BannerText;
        UpdateBar.Classes.Set("warning", UpdateBannerLayout.IsWarning(updates.Status));
        UpdateBar.IsVisible = updates.IsBannerVisible;
    }

    private void OnBannerDetails(object? sender, RoutedEventArgs e) => OpenUpdateWindow();

    private void OnBannerDownload(object? sender, RoutedEventArgs e) => _updates?.DownloadCommand.Execute(null);

    private void OnBannerRestart(object? sender, RoutedEventArgs e) => _updates?.RestartToUpdateCommand.Execute(null);

    private void OnBannerRetry(object? sender, RoutedEventArgs e) => _updates?.RetryCommand.Execute(null);

    private void OnBannerClose(object? sender, RoutedEventArgs e) => _updates?.LaterCommand.Execute(null);

    private void OpenUpdateWindow()
    {
        if (_updates is not { } updates)
            return;
        if (_updateWindow is { } open)
        {
            open.Activate();
            return;
        }

        var window = new UpdateWindow(updates);
        _updateWindow = window;
        window.Closed += (_, _) => _updateWindow = null;
        window.ShowDialog(this);
    }

    private void OnAboutClicked(object? sender, RoutedEventArgs e)
    {
        if (_about is not null)
            new AboutWindow(_about).ShowDialog(this);
    }

    private async void OnCheckForUpdatesClicked(object? sender, RoutedEventArgs e)
    {
        if (_updates is not { } updates)
            return;
        if (updates.CheckNowCommand.CanExecute(null))
            await updates.CheckNowCommand.ExecuteAsync(null);
        if (updates.Status is UpdateStatus.Available or UpdateStatus.Downloading or UpdateStatus.Ready)
        {
            OpenUpdateWindow();
            return;
        }

        new Flyout { Content = new TextBlock { Text = updates.StatusText, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap, MaxWidth = 360 } }.ShowAt(TitleBar);
    }

    /// <summary>Starts the update flow once the window is shown: the scheduled check and the one-time question; never blocks the first paint.</summary>
    public async void StartUpdates()
    {
        if (_updates is not { } updates || UpdateStartup.IsDisabled())
            return;
        await updates.StartAsync();
        if (updates.ShouldAskFrequency && await UpdateAskWindow.ShowAsync(this) is { } frequency)
            await updates.ChooseFrequencyAsync(frequency);
    }
}
