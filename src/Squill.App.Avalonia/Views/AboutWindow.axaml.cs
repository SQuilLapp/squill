using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Squill.Presentation.Updates;

namespace Squill.App.Avalonia.Views;

/// <summary>Modal window that shows what is installed and drives the update check; Esc closes it.</summary>
public sealed partial class AboutWindow : Window
{
    private readonly AboutViewModel? _about;

    /// <summary>Creates the window for the designer; it shows nothing.</summary>
    public AboutWindow()
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

    /// <summary>Creates the window bound to the About view-model.</summary>
    /// <param name="about">What to show.</param>
    public AboutWindow(AboutViewModel about)
        : this()
    {
        _about = about;
        ProductName.Text = about.ProductName;
        VersionText.Text = about.VersionText;
        ChannelText.Text = about.ChannelText;
        InstallKindText.Text = about.InstallKindText;
        RuntimeText.Text = about.RuntimeText;
        LicenseText.Text = about.LicenseText;
        DataFolderPath.Text = about.DataFolder;
        if (about.ShowWhatsNew)
        {
            WhatsNewText.Text = about.WhatsNewText;
            NotesHost.Content = ReleaseNotesPresenter.Build(about.Notes, this);
            WhatsNew.IsVisible = true;
        }

        about.Updates.PropertyChanged += OnUpdatesChanged;
        RefreshStatus();
        Closed += (_, _) => about.Updates.PropertyChanged -= OnUpdatesChanged;
    }

    private void OnUpdatesChanged(object? sender, PropertyChangedEventArgs e) => RefreshStatus();

    private void RefreshStatus()
    {
        var updates = _about!.Updates;
        StatusText.Text = updates.StatusText;
        CheckButton.IsEnabled = updates.CanCheck;
        RestartButton.IsVisible = updates.CanRestart;
    }

    private void OnCheckNow(object? sender, RoutedEventArgs e) => _about?.Updates.CheckNowCommand.Execute(null);

    private void OnRestart(object? sender, RoutedEventArgs e) => _about?.Updates.RestartToUpdateCommand.Execute(null);

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private async void OnOpenDataFolder(object? sender, RoutedEventArgs e)
    {
        if (_about is { } about && Directory.Exists(about.DataFolder))
            await Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(about.DataFolder));
    }

    private async void OnOpenRepository(object? sender, RoutedEventArgs e)
    {
        if (_about is { } about)
            await Launcher.LaunchUriAsync(new Uri(about.RepositoryUrl));
    }
}
