using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Squill.App.WinUI.Views;
using Squill.Platform.Updates;
using Squill.Presentation.Localization;
using Squill.Presentation.Updates;

namespace Squill.App.WinUI;

public sealed partial class MainWindow
{
    private UpdateWindow? _updateWindow;
    private TeachingTip? _checkTip;
    private Button _bannerDetails = null!;
    private Button _bannerDownload = null!;
    private Button _bannerRestart = null!;
    private Button _bannerRetry = null!;
    private ProgressBar _bannerProgress = null!;

    private UpdateViewModel Updates => _services!.Updates;

    private void AttachUpdates()
    {
        _bannerDetails = BannerButton("update.button.details", OnBannerDetails);
        _bannerDownload = BannerButton("update.button.download", (_, _) => Updates.DownloadCommand.Execute(null));
        _bannerRestart = BannerButton("update.button.restart", (_, _) => Updates.RestartToUpdateCommand.Execute(null));
        _bannerRetry = BannerButton("update.button.retry", (_, _) => Updates.RetryCommand.Execute(null));
        _bannerProgress = new ProgressBar { Width = 160, Minimum = 0, Maximum = 100, VerticalAlignment = VerticalAlignment.Center };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 8) };
        foreach (var part in new FrameworkElement[] { _bannerDetails, _bannerDownload, _bannerProgress, _bannerRestart, _bannerRetry })
            actions.Children.Add(part);
        UpdateBar.Content = actions;
        Updates.PropertyChanged += OnUpdatesChanged;
        RefreshBanner();
    }

    private static Button BannerButton(string key, RoutedEventHandler click)
    {
        var button = new Button { Content = UiText.Get(key) };
        AutomationProperties.SetName(button, UiText.Get(key));
        button.Click += click;
        return button;
    }

    private void OnUpdatesChanged(object? sender, PropertyChangedEventArgs e) => RefreshBanner();

    private void RefreshBanner()
    {
        var parts = UpdateBannerLayout.PartsFor(Updates.Status);
        _bannerDetails.Visibility = Show(parts.HasFlag(BannerParts.Details));
        _bannerDownload.Visibility = Show(parts.HasFlag(BannerParts.Download));
        _bannerProgress.Visibility = Show(parts.HasFlag(BannerParts.Progress));
        _bannerRestart.Visibility = Show(parts.HasFlag(BannerParts.Restart));
        _bannerRetry.Visibility = Show(parts.HasFlag(BannerParts.Retry));
        _bannerProgress.Value = Updates.Progress;
        UpdateBar.Severity = UpdateBannerLayout.IsWarning(Updates.Status) ? InfoBarSeverity.Warning : InfoBarSeverity.Informational;
        UpdateBar.Message = Updates.BannerText;
        UpdateBar.IsOpen = Updates.IsBannerVisible;
    }

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    // Only the close button means "later"; closing the bar from code (state change) must not hide the next offer.
    private void OnUpdateBarClosed(InfoBar sender, InfoBarClosedEventArgs args)
    {
        if (args.Reason == InfoBarCloseReason.CloseButton)
            Updates.LaterCommand.Execute(null);
    }

    private void OnBannerDetails(object sender, RoutedEventArgs e) => OpenUpdateWindow();

    private void OpenUpdateWindow()
    {
        if (_updateWindow is { } open)
        {
            open.Activate();
            return;
        }

        var window = new UpdateWindow(this, Updates);
        _updateWindow = window;
        window.Closed += (_, _) => _updateWindow = null;
        window.Activate();
    }

    /// <summary>Starts the update flow once the window is shown: the scheduled check and the one-time question; never blocks the first paint.</summary>
    public async Task StartUpdatesAsync()
    {
        if (UpdateStartup.IsDisabled())
            return;
        await Updates.StartAsync();
        if (Updates.ShouldAskFrequency && Content is FrameworkElement root && await UpdateAskDialog.ShowAsync(root) is { } frequency)
            await Updates.ChooseFrequencyAsync(frequency);
    }

    private async void OnCheckForUpdatesClicked(object sender, RoutedEventArgs e)
    {
        var updates = Updates;
        if (updates.CheckNowCommand.CanExecute(null))
            await updates.CheckNowCommand.ExecuteAsync(null);
        if (updates.Status is UpdateStatus.Available or UpdateStatus.Downloading or UpdateStatus.Ready)
        {
            OpenUpdateWindow();
            return;
        }

        _checkTip ??= NewCheckTip();
        _checkTip.Title = updates.StatusText;
        _checkTip.IsOpen = true;
    }

    private TeachingTip NewCheckTip()
    {
        var tip = new TeachingTip { Target = TitleBar, IsLightDismissEnabled = true, PreferredPlacement = TeachingTipPlacementMode.Bottom };
        Root.Children.Add(tip);
        return tip;
    }
}
