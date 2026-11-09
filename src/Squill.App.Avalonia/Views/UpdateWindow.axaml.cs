using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Squill.Platform.Updates;
using Squill.Presentation.Localization;
using Squill.Presentation.Updates;

namespace Squill.App.Avalonia.Views;

/// <summary>Modal window that presents an available update, its release notes and the download and restart steps; Esc closes it.</summary>
public sealed partial class UpdateWindow : Window
{
    private readonly UpdateViewModel? _updates;

    /// <summary>Creates the window for the designer; it shows nothing.</summary>
    public UpdateWindow()
    {
        InitializeComponent();
        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape)
                return;
            e.Handled = true;
            Close();
        };
    }

    /// <summary>Creates the window bound to the update flow.</summary>
    /// <param name="updates">The update flow to show and drive.</param>
    public UpdateWindow(UpdateViewModel updates)
        : this()
    {
        _updates = updates;
        NotesHost.Content = ReleaseNotesPresenter.Build(updates.ReleaseNotes, this);
        updates.PropertyChanged += OnUpdatesChanged;
        Refresh();
        Closed += (_, _) => updates.PropertyChanged -= OnUpdatesChanged;
    }

    private void OnUpdatesChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        var updates = _updates!;
        Title = TitleText.Text = UiText.Format("update.dialog.title", updates.AvailableVersion);
        CurrentText.Text = UiText.Format("update.dialog.current", updates.CurrentVersion);
        NewText.Text = UiText.Format("update.dialog.new", updates.AvailableVersion);
        var size = string.IsNullOrEmpty(updates.DownloadSizeText) ? string.Empty : UiText.Format("update.dialog.size", updates.DownloadSizeText);
        SizeText.Text = updates.IsDelta ? $"{size} {UiText.Get("update.dialog.delta")}".Trim() : size;
        SizeText.IsVisible = SizeText.Text.Length > 0;
        NotesPanel.IsVisible = updates.HasReleaseNotes;
        Progress.Value = updates.Progress;
        Progress.IsVisible = updates.Status == UpdateStatus.Downloading;
        StatusText.Text = updates.Status == UpdateStatus.Failed ? updates.FailureText : updates.StatusText;
        DetailsText.Text = updates.FailureDetails ?? string.Empty;
        DetailsExpander.IsVisible = updates.Status == UpdateStatus.Failed && !string.IsNullOrEmpty(updates.FailureDetails);
        DownloadButton.IsVisible = updates.CanDownload;
        RestartButton.IsVisible = updates.CanRestart;
        RetryButton.IsVisible = updates.CanRetry;
        SkipButton.IsVisible = updates.CanSkip;
    }

    private void OnDownload(object? sender, RoutedEventArgs e) => _updates?.DownloadCommand.Execute(null);

    private void OnRestart(object? sender, RoutedEventArgs e) => _updates?.RestartToUpdateCommand.Execute(null);

    private void OnRetry(object? sender, RoutedEventArgs e) => _updates?.RetryCommand.Execute(null);

    private void OnSkip(object? sender, RoutedEventArgs e)
    {
        _updates?.SkipVersionCommand.Execute(null);
        Close();
    }

    private void OnLater(object? sender, RoutedEventArgs e)
    {
        _updates?.LaterCommand.Execute(null);
        Close();
    }
}
