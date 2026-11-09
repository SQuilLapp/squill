using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Squill.Platform.Updates;
using Squill.Presentation.Localization;
using Squill.Presentation.Updates;
using Windows.System;

namespace Squill.App.WinUI.Views;

/// <summary>Modal window that presents an available update, its release notes and the download and restart steps; Esc closes it.</summary>
public sealed partial class UpdateWindow : Window
{
    private readonly Window _owner;
    private readonly UpdateViewModel _updates;

    /// <summary>Creates the window bound to the update flow.</summary>
    /// <param name="owner">The main window; it stays disabled while this window is open.</param>
    /// <param name="updates">The update flow to show and drive.</param>
    public UpdateWindow(Window owner, UpdateViewModel updates)
    {
        InitializeComponent();
        _owner = owner;
        _updates = updates;
        CaptionBar.Follow(this, Root, (FrameworkElement)owner.Content);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "squill.ico"));
        NotesHost.Content = ReleaseNotesPresenter.Build(updates.ReleaseNotes, Root);
        foreach (var (button, key) in new[]
        {
            (DownloadButton, "update.button.download"), (RestartButton, "update.button.restart"), (RetryButton, "update.button.retry"),
            (SkipButton, "update.button.skip"), (LaterButton, "update.button.later"),
        })
            AutomationProperties.SetName(button, UiText.Get(key));
        var escape = new KeyboardAccelerator { Key = VirtualKey.Escape };
        escape.Invoked += (_, args) =>
        {
            args.Handled = true;
            Close();
        };
        Root.KeyboardAccelerators.Add(escape);
        updates.PropertyChanged += OnUpdatesChanged;
        Refresh();
        ModalWindow.Place(this, owner, 520, 580, resizable: false);
        Closed += (_, _) =>
        {
            updates.PropertyChanged -= OnUpdatesChanged;
            _owner.Activate();
        };
    }

    private void OnUpdatesChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        var updates = _updates;
        Title = TitleText.Text = UiText.Format("update.dialog.title", updates.AvailableVersion);
        CurrentText.Text = UiText.Format("update.dialog.current", updates.CurrentVersion);
        NewText.Text = UiText.Format("update.dialog.new", updates.AvailableVersion);
        var size = string.IsNullOrEmpty(updates.DownloadSizeText) ? string.Empty : UiText.Format("update.dialog.size", updates.DownloadSizeText);
        SizeText.Text = updates.IsDelta ? $"{size} {UiText.Get("update.dialog.delta")}".Trim() : size;
        SizeText.Visibility = Show(SizeText.Text.Length > 0);
        NotesPanel.Visibility = Show(updates.HasReleaseNotes);
        Progress.Value = updates.Progress;
        Progress.Visibility = Show(updates.Status == UpdateStatus.Downloading);
        StatusText.Text = updates.Status == UpdateStatus.Failed ? updates.FailureText : updates.StatusText;
        DetailsText.Text = updates.FailureDetails ?? string.Empty;
        DetailsExpander.Visibility = Show(updates.Status == UpdateStatus.Failed && !string.IsNullOrEmpty(updates.FailureDetails));
        DownloadButton.Visibility = Show(updates.CanDownload);
        RestartButton.Visibility = Show(updates.CanRestart);
        RetryButton.Visibility = Show(updates.CanRetry);
        SkipButton.Visibility = Show(updates.CanSkip);
    }

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private void OnDownload(object sender, RoutedEventArgs e) => _updates.DownloadCommand.Execute(null);

    private void OnRestart(object sender, RoutedEventArgs e) => _updates.RestartToUpdateCommand.Execute(null);

    private void OnRetry(object sender, RoutedEventArgs e) => _updates.RetryCommand.Execute(null);

    private void OnSkip(object sender, RoutedEventArgs e)
    {
        _updates.SkipVersionCommand.Execute(null);
        Close();
    }

    private void OnLater(object sender, RoutedEventArgs e)
    {
        _updates.LaterCommand.Execute(null);
        Close();
    }
}
